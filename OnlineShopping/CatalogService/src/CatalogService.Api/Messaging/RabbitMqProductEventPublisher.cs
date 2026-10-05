using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

using RabbitMQ.Client;

using ShoppingTelemetry;

namespace CatalogService.Api.Messaging;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes", Justification = "Instantiated by the DI container.")]
internal sealed partial class RabbitMqProductEventPublisher : IProductEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqProductEventPublisher> _logger;

    public RabbitMqProductEventPublisher(IConfiguration configuration, ILogger<RabbitMqProductEventPublisher> logger)
    {
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

    public Task PublishUpsertedAsync(ProductChangedMessage message, CancellationToken cancellationToken = default) => PublishAsync(RabbitMqTopology.ChangedRoutingKey, message, cancellationToken);

    public Task PublishDeletedAsync(Guid id, CancellationToken cancellationToken = default) => PublishAsync(RabbitMqTopology.DeletedRoutingKey, new ProductDeletedMessage(id), cancellationToken);

    private async Task PublishAsync<TMessage>(string routingKey, TMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string messageId = Guid.NewGuid().ToString("N");
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["messaging.message.id"] = messageId,
            ["messaging.destination.name"] = RabbitMqTopology.ExchangeName,
            ["messaging.rabbitmq.destination.routing_key"] = routingKey
        });

        try
        {
            await PublishCoreAsync(routingKey, message, messageId, cancellationToken).ConfigureAwait(false);
            ShoppingTelemetryMetrics.RecordMessagingOperation("catalog-service", "publish", routingKey, "success");
            Log.ProductEventPublished(_logger, routingKey, messageId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            ShoppingTelemetryMetrics.RecordMessagingOperation("catalog-service", "publish", routingKey, "failure");
            Log.ProductEventPublishFailed(_logger, routingKey, messageId, exception.GetType().Name);
            throw;
        }
    }

    private async Task PublishCoreAsync<TMessage>(string routingKey, TMessage message, string messageId, CancellationToken cancellationToken)
    {

        IConnection connection = await _connectionFactory.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable connectionScope = connection.ConfigureAwait(false);
        IChannel channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using ConfiguredAsyncDisposable channelScope = channel.ConfigureAwait(false);

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

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOptions));
        var properties = new BasicProperties
        {
            MessageId = messageId,
            Persistent = true,
            ContentType = "application/json"
        };

        await channel.BasicPublishAsync(RabbitMqTopology.ExchangeName, routingKey, mandatory: false, properties, body, cancellationToken).ConfigureAwait(false);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Published product event to routing key {RoutingKey} with message ID {MessageId}.")]
        public static partial void ProductEventPublished(ILogger logger, string routingKey, string messageId);

        [LoggerMessage(EventId = 2, Level = LogLevel.Error, Message = "Failed to publish product event to routing key {RoutingKey} with message ID {MessageId}; error type {ErrorType}.")]
        public static partial void ProductEventPublishFailed(ILogger logger, string routingKey, string messageId, string errorType);
    }
}
