using CartService.Bll;
using CartService.Dal;

namespace CartService.Tests;

public class LiteDbCartRepositoryTests
{
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

                await repository.UpsertAsync(cart);
            }

            using (var repository = new LiteDbCartRepository(databasePath))
            {
                Cart? cart = await repository.GetByIdAsync(expectedCartKey);

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
