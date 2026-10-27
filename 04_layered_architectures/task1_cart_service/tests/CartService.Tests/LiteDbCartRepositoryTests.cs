using CartService.Bll;
using CartService.Dal;

namespace CartService.Tests;

public class LiteDbCartRepositoryTests
{
    [Fact]
    public async Task UpsertAsync_ThenGetByIdAsync_ReturnsPersistedCart()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"cart-{Guid.NewGuid():N}.db");

        try
        {
            var expectedCartId = Guid.NewGuid();

            using (var repository = new LiteDbCartRepository(databasePath))
            {
                var cart = new Cart(expectedCartId);
                cart.AddItem(new CartItem(10, "Mouse", new CartItemImage("https://example.com/mouse.png", "Mouse"), 25.50m, 2));

                await repository.UpsertAsync(cart);
            }

            using (var repository = new LiteDbCartRepository(databasePath))
            {
                var cart = await repository.GetByIdAsync(expectedCartId);

                Assert.NotNull(cart);
                Assert.Equal(expectedCartId, cart!.Id);
                var item = Assert.Single(cart.GetItems());
                Assert.Equal(10, item.Id);
                Assert.Equal("Mouse", item.Name);
                Assert.Equal(2, item.Quantity);
                Assert.Equal(25.50m, item.Price);
                Assert.NotNull(item.Image);
                Assert.Equal("https://example.com/mouse.png", item.Image!.Url);
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