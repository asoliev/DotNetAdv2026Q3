using System.Diagnostics;

using CartService.Bll;
using CartService.Dal;

namespace CartService.Tests;

public class LiteDbCartRepositoryTests
{
    [Fact]
    public async Task RepositoryOperationsEmitDatabaseActivityWithoutCartIdentifiers()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cart-{Guid.NewGuid():N}.db");
        Activity? capturedActivity = null;
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "CartService.Dal.LiteDbCartRepository",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => capturedActivity = activity
        };
        ActivitySource.AddActivityListener(listener);

        try
        {
            using var repository = new LiteDbCartRepository(databasePath);
            await repository.GetByIdAsync("private-cart-key", TestContext.Current.CancellationToken);

            Assert.NotNull(capturedActivity);
            Assert.Equal("cart.repository.get_by_id", capturedActivity!.DisplayName);
            Assert.Equal("litedb", capturedActivity.GetTagItem("db.system"));
            Assert.Equal("get_by_id", capturedActivity.GetTagItem("db.operation.name"));
            Assert.DoesNotContain(capturedActivity.TagObjects, tag => tag.Key.Contains("cart", StringComparison.OrdinalIgnoreCase) && tag.Key != "db.system");
            Assert.DoesNotContain(capturedActivity.TagObjects, tag => Equals(tag.Value, "private-cart-key"));
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task UpsertAsyncThenGetByIdAsyncReturnsPersistedCart()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cart-{Guid.NewGuid():N}.db");

        try
        {
            var expectedCartKey = Guid.NewGuid().ToString("N");
            var itemId = Guid.NewGuid();

            using (var repository = new LiteDbCartRepository(databasePath))
            {
                var cart = new Cart(expectedCartKey);
                cart.AddItem(new CartItem(itemId, "Mouse", new CartItemImage(new Uri("https://example.com/mouse.png"), "Mouse"), 25.50m, 2));

                await repository.UpsertAsync(cart, TestContext.Current.CancellationToken);
            }

            using (var repository = new LiteDbCartRepository(databasePath))
            {
                Cart? cart = await repository.GetByIdAsync(expectedCartKey, TestContext.Current.CancellationToken);

                Assert.NotNull(cart);
                Assert.Equal(expectedCartKey, cart!.Id);
                CartItem item = Assert.Single(cart.GetItems());
                Assert.Equal(itemId, item.Id);
                Assert.Equal("Mouse", item.Name);
                Assert.Equal(2, item.Quantity);
                Assert.Equal(25.50m, item.Price);
                Assert.NotNull(item.Image);
                Assert.Equal(new Uri("https://example.com/mouse.png"), item.Image!.Url);
            }
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
