using System.Runtime.CompilerServices;
using System.Text.Json;

using CartService.Bll;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

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
        _connectionFactory = new ConnectionFactory
        {
            HostName = configuration["RabbitMq:Host"] ?? "localhost",
            UserName = configuration["RabbitMq:Username"] ?? "guest",
            Password = configuration["RabbitMq:Password"] ?? "guest"
        };
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
            try
            {
                await ProcessMessageAsync(eventArgs, stoppingToken).ConfigureAwait(false);
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, stoppingToken).ConfigureAwait(false);
            }
            catch (JsonException exception)
            {
                Log.FailedToProcessProductChangeMessage(_logger, exception);
                await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                Log.FailedToProcessProductChangeMessage(_logger, exception);
                await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken).ConfigureAwait(false);
            }
            catch (ArgumentException exception)
            {
                Log.FailedToProcessProductChangeMessage(_logger, exception);
                await channel.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: false, stoppingToken).ConfigureAwait(false);
            }
        };

        await channel.BasicConsumeAsync(RabbitMqTopology.QueueName, autoAck: false, consumer, stoppingToken).ConfigureAwait(false);

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    private async Task ProcessMessageAsync(BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (eventArgs.RoutingKey == RabbitMqTopology.DeletedRoutingKey)
        {
            ProductDeletedMessage deleted = JsonSerializer.Deserialize<ProductDeletedMessage>(eventArgs.Body.Span, JsonOptions)
                ?? throw new InvalidOperationException("Product delete message is invalid.");
            await _cartManager.RemoveCatalogItemAsync(deleted.Id, cancellationToken).ConfigureAwait(false);
            return;
        }

        ProductChangedMessage changed = JsonSerializer.Deserialize<ProductChangedMessage>(eventArgs.Body.Span, JsonOptions)
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
        [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to process product change message.")]
        public static partial void FailedToProcessProductChangeMessage(ILogger logger, Exception exception);
    }
}
