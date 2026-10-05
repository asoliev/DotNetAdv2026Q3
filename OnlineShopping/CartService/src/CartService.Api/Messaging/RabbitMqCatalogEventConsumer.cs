using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;

using CartService.Bll;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using ShoppingTelemetry;

namespace CartService.Api.Messaging;

public sealed partial class RabbitMqCatalogEventConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CartManager _cartManager;
    private readonly ILogger<RabbitMqCatalogEventConsumer> _logger;
    private readonly ConnectionFactory _connectionFactory;

    public RabbitMqCatalogEventConsumer(CartManager cartManager, ILogger<RabbitMqCatalogEventConsumer> logger, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        _cartManager = cartManager ?? throw new ArgumentNullException(nameof(cartManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionFactory = new ConnectionFactory { HostName = configuration["RabbitMq:Host"] ?? "localhost" };

        // Credentials come from environment variables or user-secrets. Without them the client's built-in
        // guest login is used, which RabbitMQ only accepts from localhost.
        if (configuration["RabbitMq:Username"] is { } userName)
        {
            _connectionFactory.UserName = userName;
        }

        if (configuration["RabbitMq:Password"] is { } password)
        {
            _connectionFactory.Password = password;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        IConnection connection = await _connectionFactory.CreateConnectionAsync(stoppingToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable connectionScope = connection.ConfigureAwait(false);
        IChannel channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable channelScope = channel.ConfigureAwait(false);

        await DeclareTopologyAsync(channel, stoppingToken).ConfigureAwait(false);

        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            using IDisposable? scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["messaging.message.id"] = eventArgs.BasicProperties.MessageId,
                ["messaging.destination.name"] = RabbitMqTopology.QueueName,
                ["messaging.rabbitmq.destination.routing_key"] = eventArgs.RoutingKey,
                ["messaging.rabbitmq.message.redelivered"] = eventArgs.Redelivered
            });

            if (await TryProcessMessageAsync(eventArgs.RoutingKey, eventArgs.Body, stoppingToken).ConfigureAwait(false))
            {
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken).ConfigureAwait(false);
                ShoppingTelemetryMetrics.RecordMessagingOperation("cart-service", "settle", eventArgs.RoutingKey, "ack");
                Log.ProductEventMessageAcked(_logger, eventArgs.RoutingKey);
                return;
            }

            await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken).ConfigureAwait(false);
            ShoppingTelemetryMetrics.RecordMessagingOperation("cart-service", "settle", eventArgs.RoutingKey, "nack");
            Log.ProductEventMessageNacked(_logger, eventArgs.RoutingKey);
        };

        await channel.BasicConsumeAsync(RabbitMqTopology.QueueName, autoAck: false, consumer, stoppingToken).ConfigureAwait(false);

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies a catalog event to the carts. Returns <see langword="true"/> when the message should be acked,
    /// or <see langword="false"/> (after logging) when it is invalid and should be nacked to the retry queue.
    /// </summary>
    internal async Task<bool> TryProcessMessageAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await ProcessMessageAsync(routingKey, body, cancellationToken).ConfigureAwait(false);
            Activity.Current?.SetTag("messaging.operation.name", "process");
            Activity.Current?.SetTag("messaging.message.outcome", "success");
            ShoppingTelemetryMetrics.RecordMessagingOperation("cart-service", "process", GetEventType(routingKey), "success");
            ShoppingTelemetryMetrics.RecordMessageProcessingDuration(GetEventType(routingKey), "success", Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return true;
        }
        catch (JsonException exception)
        {
            RecordProcessingFailure(routingKey, exception, startedAt);
        }
        catch (InvalidOperationException exception)
        {
            RecordProcessingFailure(routingKey, exception, startedAt);
        }
        catch (ArgumentException exception)
        {
            RecordProcessingFailure(routingKey, exception, startedAt);
        }

        return false;
    }

    private void RecordProcessingFailure(string routingKey, Exception exception, long startedAt)
    {
        Activity? activity = Activity.Current;
        activity?.SetTag("messaging.operation.name", "process");
        activity?.SetTag("messaging.message.outcome", "failure");
        activity?.SetTag("error.type", exception.GetType().Name);
        activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
        ShoppingTelemetryMetrics.RecordMessagingOperation("cart-service", "process", GetEventType(routingKey), "failure");
        ShoppingTelemetryMetrics.RecordMessageProcessingDuration(GetEventType(routingKey), "failure", Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        Log.FailedToProcessProductChangeMessage(_logger, routingKey, exception.GetType().Name);
    }

    private static string GetEventType(string routingKey) => routingKey switch
    {
        RabbitMqTopology.ChangedRoutingKey => "product.changed",
        RabbitMqTopology.DeletedRoutingKey => "product.deleted",
        _ => "unknown"
    };

    private async Task ProcessMessageAsync(string routingKey, ReadOnlyMemory<byte> body, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (routingKey == RabbitMqTopology.DeletedRoutingKey)
        {
            ProductDeletedMessage deleted = JsonSerializer.Deserialize<ProductDeletedMessage>(body.Span, JsonOptions)
                ?? throw new InvalidOperationException("Product delete message is invalid.");
            await _cartManager.RemoveCatalogItemAsync(deleted.Id, cancellationToken).ConfigureAwait(false);
            return;
        }

        ProductChangedMessage changed = JsonSerializer.Deserialize<ProductChangedMessage>(body.Span, JsonOptions)
            ?? throw new InvalidOperationException("Product change message is invalid.");

        CartItemImage? image = changed.Image is null ? null : new CartItemImage(changed.Image.Url, changed.Image.AltText);
        await _cartManager.UpdateCatalogItemAsync(changed.Id, changed.Name, image, changed.Price, cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(RabbitMqTopology.ExchangeName, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.ExchangeDeclareAsync(RabbitMqTopology.RetryExchangeName, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueDeclareAsync(
            RabbitMqTopology.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = RabbitMqTopology.RetryExchangeName
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueDeclareAsync(
            RabbitMqTopology.RetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-message-ttl"] = 5000,
                ["x-dead-letter-exchange"] = RabbitMqTopology.ExchangeName
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await channel.QueueBindAsync(RabbitMqTopology.QueueName, RabbitMqTopology.ExchangeName, RabbitMqTopology.ChangedRoutingKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.QueueBindAsync(RabbitMqTopology.QueueName, RabbitMqTopology.ExchangeName, RabbitMqTopology.DeletedRoutingKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.QueueBindAsync(RabbitMqTopology.RetryQueueName, RabbitMqTopology.RetryExchangeName, RabbitMqTopology.ChangedRoutingKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        await channel.QueueBindAsync(RabbitMqTopology.RetryQueueName, RabbitMqTopology.RetryExchangeName, RabbitMqTopology.DeletedRoutingKey, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to process product event from routing key {RoutingKey}; error type {ErrorType}.")]
        public static partial void FailedToProcessProductChangeMessage(ILogger logger, string routingKey, string errorType);

        [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Acknowledged product event from routing key {RoutingKey}.")]
        public static partial void ProductEventMessageAcked(ILogger logger, string routingKey);

        [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Rejected product event from routing key {RoutingKey} for retry.")]
        public static partial void ProductEventMessageNacked(ILogger logger, string routingKey);
    }
}
