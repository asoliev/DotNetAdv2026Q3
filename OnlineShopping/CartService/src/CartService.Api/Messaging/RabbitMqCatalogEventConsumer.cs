using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CartService.Api.Messaging;

internal sealed class RabbitMqCatalogEventConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CartService.Bll.CartService _cartService;
    private readonly ILogger<RabbitMqCatalogEventConsumer> _logger;
    private readonly ConnectionFactory _connectionFactory = new()
    {
        HostName = "localhost",
        UserName = "guest",
        Password = "guest",
        DispatchConsumersAsync = true
    };

    public RabbitMqCatalogEventConsumer(CartService.Bll.CartService cartService, ILogger<RabbitMqCatalogEventConsumer> logger)
    {
        _cartService = cartService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        DeclareTopology(channel);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.Received += async (_, eventArgs) =>
        {
            try
            {
                await ProcessMessageAsync(channel, eventArgs, stoppingToken);
                channel.BasicAck(eventArgs.DeliveryTag, multiple: false);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to process product change message.");
                channel.BasicNack(eventArgs.DeliveryTag, multiple: false, requeue: false);
            }
        };

        channel.BasicConsume(RabbitMqTopology.QueueName, autoAck: false, consumer);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ProcessMessageAsync(IModel channel, BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (eventArgs.RoutingKey == RabbitMqTopology.DeletedRoutingKey)
        {
            var deleted = JsonSerializer.Deserialize<ProductDeletedMessage>(eventArgs.Body.Span, JsonOptions)
                ?? throw new InvalidOperationException("Product delete message is invalid.");
            await _cartService.RemoveCatalogItemAsync(deleted.Id, cancellationToken);
            return;
        }

        var changed = JsonSerializer.Deserialize<ProductChangedMessage>(eventArgs.Body.Span, JsonOptions)
            ?? throw new InvalidOperationException("Product change message is invalid.");

        var image = changed.Image is null ? null : new CartService.Bll.CartItemImage(changed.Image.Url, changed.Image.AltText);
        await _cartService.UpdateCatalogItemAsync(changed.Id, changed.Name, image, changed.Price, cancellationToken);
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
}