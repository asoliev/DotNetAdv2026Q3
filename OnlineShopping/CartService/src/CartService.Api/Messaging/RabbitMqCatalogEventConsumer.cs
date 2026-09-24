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
    private readonly ConnectionFactory _connectionFactory = new()
    {
        HostName = "localhost",
        UserName = "guest",
        Password = GetRabbitMqPassword(),
        DispatchConsumersAsync = true
    };

    public RabbitMqCatalogEventConsumer(CartManager cartManager, ILogger<RabbitMqCatalogEventConsumer> logger)
    {
        _cartManager = cartManager ?? throw new ArgumentNullException(nameof(cartManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using IConnection connection = _connectionFactory.CreateConnection();
        using IModel channel = connection.CreateModel();

        DeclareTopology(channel);

        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.Received += async (_, eventArgs) =>
        {
            try
            {
                await ProcessMessageAsync(eventArgs, stoppingToken).ConfigureAwait(false);
                channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            catch (JsonException exception)
            {
                Log.FailedToProcessProductChangeMessage(_logger, exception);
                channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
            }
            catch (InvalidOperationException exception)
            {
                Log.FailedToProcessProductChangeMessage(_logger, exception);
                channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
            }
            catch (ArgumentException exception)
            {
                Log.FailedToProcessProductChangeMessage(_logger, exception);
                channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
            }
        };

        channel.BasicConsume(RabbitMqTopology.QueueName, autoAck: false, consumer);

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

    private static void DeclareTopology(IModel channel)
    {
        channel.ExchangeDeclare(RabbitMqTopology.ExchangeName, ExchangeType.Direct, durable: true, autoDelete: false);
        channel.ExchangeDeclare(RabbitMqTopology.RetryExchangeName, ExchangeType.Direct, durable: true, autoDelete: false);

        channel.QueueDeclare(
            RabbitMqTopology.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = RabbitMqTopology.RetryExchangeName
            });

        channel.QueueDeclare(
            RabbitMqTopology.RetryQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-message-ttl"] = 5000,
                ["x-dead-letter-exchange"] = RabbitMqTopology.ExchangeName
            });

        channel.QueueBind(RabbitMqTopology.QueueName, RabbitMqTopology.ExchangeName, RabbitMqTopology.ChangedRoutingKey);
        channel.QueueBind(RabbitMqTopology.QueueName, RabbitMqTopology.ExchangeName, RabbitMqTopology.DeletedRoutingKey);
        channel.QueueBind(RabbitMqTopology.RetryQueueName, RabbitMqTopology.RetryExchangeName, RabbitMqTopology.ChangedRoutingKey);
        channel.QueueBind(RabbitMqTopology.RetryQueueName, RabbitMqTopology.RetryExchangeName, RabbitMqTopology.DeletedRoutingKey);
    }

    private static string GetRabbitMqPassword()
    {
        return Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD")
            ?? throw new InvalidOperationException("RABBITMQ_PASSWORD environment variable is required.");
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Error, Message = "Failed to process product change message.")]
        public static partial void FailedToProcessProductChangeMessage(ILogger logger, Exception exception);
    }
}
