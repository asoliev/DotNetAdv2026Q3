namespace CartService.Api.Messaging;

internal static class RabbitMqTopology
{
    public const string ExchangeName = "online-shopping.catalog.products";
    public const string RetryExchangeName = "online-shopping.catalog.products.retry";
    public const string ChangedRoutingKey = "product.changed";
    public const string DeletedRoutingKey = "product.deleted";
    public const string QueueName = "online-shopping.cart.products";
    public const string RetryQueueName = "online-shopping.cart.products.retry";
}