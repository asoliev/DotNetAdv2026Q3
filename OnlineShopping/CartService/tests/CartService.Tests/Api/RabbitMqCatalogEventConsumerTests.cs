using System.Diagnostics;
using System.Text;
using System.Text.Json;

using CartService.Api.Messaging;
using CartService.Bll;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CartService.Tests.Api;

public sealed class RabbitMqCatalogEventConsumerTests(CartApiFactory factory) : IClassFixture<CartApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private CartManager Manager => factory.Services.GetRequiredService<CartManager>();

    [Fact]
    public async Task ProductChangedMessageUpdatesItemsInCarts()
    {
        var itemId = Guid.NewGuid();
        var cartKey = await AddItemToNewCartAsync(itemId);
        var message = new ProductChangedMessage(itemId, "Phone 2", null, new ProductImageMessage(new Uri("https://example.com/phone2.png"), "Phone 2"), Guid.NewGuid(), 399m, 7);

        var acked = await ProcessAsync("product.changed", JsonSerializer.SerializeToUtf8Bytes(message, JsonSerializerOptions.Web));

        Assert.True(acked);
        CartItem item = Assert.Single(await Manager.GetItemsAsync(cartKey, Ct));
        Assert.Equal(("Phone 2", 399m, 2), (item.Name, item.Price, item.Quantity));
        Assert.Equal(new Uri("https://example.com/phone2.png"), item.Image?.Url);
    }

    [Fact]
    public async Task ProductChangedMessageWithoutImageClearsTheImage()
    {
        var itemId = Guid.NewGuid();
        var cartKey = await AddItemToNewCartAsync(itemId);
        var message = new ProductChangedMessage(itemId, "Phone", null, null, Guid.NewGuid(), 10m, 1);

        Assert.True(await ProcessAsync("product.changed", JsonSerializer.SerializeToUtf8Bytes(message, JsonSerializerOptions.Web)));
        Assert.Null(Assert.Single(await Manager.GetItemsAsync(cartKey, Ct)).Image);
    }

    [Fact]
    public async Task ProductDeletedMessageRemovesItemsFromCarts()
    {
        var itemId = Guid.NewGuid();
        var cartKey = await AddItemToNewCartAsync(itemId);

        var acked = await ProcessAsync("product.deleted", JsonSerializer.SerializeToUtf8Bytes(new ProductDeletedMessage(itemId), JsonSerializerOptions.Web));

        Assert.True(acked);
        Assert.Empty(await Manager.GetItemsAsync(cartKey, Ct));
    }

    [Theory]
    [InlineData("product.changed", "{ not json")]
    [InlineData("product.changed", "null")]
    [InlineData("product.deleted", "null")]
    [InlineData("product.changed", """{"id":"00000000-0000-0000-0000-000000000000","name":"","price":1}""")]
    public async Task InvalidMessagesAreRejected(string routingKey, string body)
    {
        var acked = await ProcessAsync(routingKey, Encoding.UTF8.GetBytes(body));

        Assert.False(acked);
    }

    [Fact]
    public async Task InvalidMessageMarksCurrentDeliveryActivityAsError()
    {
        using var activity = new Activity("rabbitmq.receive");
        activity.Start();
        using var consumer = new RabbitMqCatalogEventConsumer(Manager, NullLogger<RabbitMqCatalogEventConsumer>.Instance, new ConfigurationBuilder().Build());

        var acked = await consumer.TryProcessMessageAsync("product.changed", Encoding.UTF8.GetBytes("{ not json"), Ct);

        Assert.False(acked);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("JsonException", activity.GetTagItem("error.type"));
        Assert.Equal("failure", activity.GetTagItem("messaging.message.outcome"));
    }

    [Fact]
    public void ConstructorRejectsMissingDependencies()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();

        Assert.Throws<ArgumentNullException>(() => new RabbitMqCatalogEventConsumer(Manager, NullLogger<RabbitMqCatalogEventConsumer>.Instance, null!));
        Assert.Throws<ArgumentNullException>(() => new RabbitMqCatalogEventConsumer(null!, NullLogger<RabbitMqCatalogEventConsumer>.Instance, configuration));
        Assert.Throws<ArgumentNullException>(() => new RabbitMqCatalogEventConsumer(Manager, null!, configuration));
    }

    private async Task<bool> ProcessAsync(string routingKey, byte[] body)
    {
        using var consumer = new RabbitMqCatalogEventConsumer(Manager, NullLogger<RabbitMqCatalogEventConsumer>.Instance, new ConfigurationBuilder().Build());
        return await consumer.TryProcessMessageAsync(routingKey, body, Ct);
    }

    private async Task<string> AddItemToNewCartAsync(Guid itemId)
    {
        var cartKey = $"cart-{Guid.NewGuid():N}";
        await Manager.AddItemAsync(cartKey, new CartItem(itemId, "Phone", new CartItemImage(new Uri("https://example.com/phone.png"), "Phone"), 499m, 2), Ct);
        return cartKey;
    }
}
