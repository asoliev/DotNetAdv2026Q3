using System.Text;
using System.Text.Json;

using RabbitMQ.Client;

namespace CatalogService.Api.Messaging;

internal sealed class RabbitMqProductEventPublisher : IProductEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConnectionFactory _connectionFactory = new()
    {
        HostName = "localhost",
        UserName = "guest",
        Password = "guest"
    };

    public Task PublishUpsertedAsync(ProductChangedMessage message, CancellationToken cancellationToken = default) => PublishAsync(RabbitMqTopology.ChangedRoutingKey, message, cancellationToken);

    public Task PublishDeletedAsync(Guid id, CancellationToken cancellationToken = default) => PublishAsync(RabbitMqTopology.DeletedRoutingKey, new ProductDeletedMessage(id), cancellationToken);

    private Task PublishAsync<TMessage>(string routingKey, TMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using IConnection connection = _connectionFactory.CreateConnection();
        using IModel channel = connection.CreateModel();

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

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOptions));
        IBasicProperties properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";

        channel.BasicPublish(RabbitMqTopology.ExchangeName, routingKey, properties, body);
        return Task.CompletedTask;
    }
}
