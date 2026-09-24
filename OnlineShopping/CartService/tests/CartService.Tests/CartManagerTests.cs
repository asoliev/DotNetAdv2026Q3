using CartService.Bll;

namespace CartService.Tests;

public class CartManagerTests
{
    [Fact]
    public async Task GetItemsAsync_WhenCartIsMissing_ReturnsEmptyList()
    {
        var repository = new FakeCartRepository();
        var manager = new CartManager(repository);

        IReadOnlyList<CartItem> items = await manager.GetItemsAsync("cart-1");

        Assert.Empty(items);
    }

    [Fact]
    public async Task AddItemAsync_CreatesCartAndPersistsItem()
    {
        var repository = new FakeCartRepository();
        var manager = new CartManager(repository);
        var item = new CartItem(Guid.NewGuid(), "Keyboard", new CartItemImage(new Uri("https://example.com/keyboard.png"), "Keyboard"), 99.99m, 1);

        IReadOnlyList<CartItem> items = await manager.AddItemAsync("cart-1", item);

        Assert.Single(items);
        Assert.Equal("cart-1", repository.GetStoredCart("cart-1")!.Id);
        Assert.Equal("Keyboard", repository.GetStoredCart("cart-1")!.GetItems().Single().Name);
    }

    [Fact]
    public async Task RemoveItemAsync_WhenCartIsMissing_ReturnsFalse()
    {
        var repository = new FakeCartRepository();
        var manager = new CartManager(repository);

        bool removed = await manager.RemoveItemAsync("cart-1", Guid.NewGuid());

        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveItemAsync_WhenItemIsMissing_ReturnsFalse()
    {
        var repository = new FakeCartRepository(new Cart("cart-1"));
        var manager = new CartManager(repository);

        bool removed = await manager.RemoveItemAsync("cart-1", Guid.NewGuid());

        Assert.False(removed);
    }

    [Fact]
    public async Task RemoveItemAsync_WhenItemExists_RemovesAndPersistsCart()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCartRepository(new Cart("cart-1", new[] { new CartItem(itemId, "Keyboard", null, 99.99m, 1) }));
        var manager = new CartManager(repository);

        bool removed = await manager.RemoveItemAsync("cart-1", itemId);

        Assert.True(removed);
        Assert.Empty(repository.GetStoredCart("cart-1")!.GetItems());
    }

    [Fact]
    public async Task UpdateCatalogItemAsync_WhenItemExists_UpdatesMatchingCarts()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCartRepository(
            new Cart("cart-1", new[] { new CartItem(itemId, "Keyboard", null, 99.99m, 1) }),
            new Cart("cart-2", new[] { new CartItem(Guid.NewGuid(), "Mouse", null, 25.00m, 1) }));
        var manager = new CartManager(repository);
        var image = new CartItemImage(new Uri("https://example.com/keyboard.png"), "Keyboard");

        await manager.UpdateCatalogItemAsync(itemId, "Mechanical Keyboard", image, 129.99m);

        CartItem updatedItem = repository.GetStoredCart("cart-1")!.GetItems().Single();
        Assert.Equal("Mechanical Keyboard", updatedItem.Name);
        Assert.Equal(129.99m, updatedItem.Price);
        Assert.Equal(image.Url, updatedItem.Image!.Url);
        Assert.Equal("Mouse", repository.GetStoredCart("cart-2")!.GetItems().Single().Name);
    }

    [Fact]
    public async Task RemoveCatalogItemAsync_WhenItemExists_RemovesMatchingCarts()
    {
        var itemId = Guid.NewGuid();
        var repository = new FakeCartRepository(
            new Cart("cart-1", new[] { new CartItem(itemId, "Keyboard", null, 99.99m, 1) }),
            new Cart("cart-2", new[] { new CartItem(Guid.NewGuid(), "Mouse", null, 25.00m, 1) }));
        var manager = new CartManager(repository);

        await manager.RemoveCatalogItemAsync(itemId);

        Assert.Empty(repository.GetStoredCart("cart-1")!.GetItems());
        Assert.Single(repository.GetStoredCart("cart-2")!.GetItems());
    }

    [Fact]
    public async Task GuardClauses_ThrowForInvalidArguments()
    {
        var manager = new CartManager(new FakeCartRepository());

        await Assert.ThrowsAsync<ArgumentException>(() => manager.GetItemsAsync(""));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.AddItemAsync(" ", new CartItem(Guid.NewGuid(), "Keyboard", null, 99.99m, 1)));
        await Assert.ThrowsAsync<ArgumentNullException>(() => manager.AddItemAsync("cart-1", null!));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.UpdateCatalogItemAsync(Guid.Empty, "Keyboard", null, 99.99m));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.UpdateCatalogItemAsync(Guid.NewGuid(), "", null, 99.99m));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.UpdateCatalogItemAsync(Guid.NewGuid(), "Keyboard", null, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => manager.RemoveCatalogItemAsync(Guid.Empty));
    }

    private sealed class FakeCartRepository : ICartRepository
    {
        private readonly Dictionary<string, Cart> _carts = new(StringComparer.OrdinalIgnoreCase);

        public FakeCartRepository(params Cart[] carts)
        {
            foreach (Cart cart in carts)
            {
                _carts[cart.Id] = cart;
            }
        }

        public Cart? GetStoredCart(string cartKey) => _carts.TryGetValue(cartKey, out Cart? cart) ? cart : null;

        public Task<Cart?> GetByIdAsync(string cartKey, CancellationToken cancellationToken = default)
        {
            _carts.TryGetValue(cartKey, out Cart? cart);
            return Task.FromResult(cart);
        }

        public Task<IReadOnlyList<Cart>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Cart> carts = _carts.Values.ToList();
            return Task.FromResult(carts);
        }

        public Task UpsertAsync(Cart cart, CancellationToken cancellationToken = default)
        {
            _carts[cart.Id] = cart;
            return Task.CompletedTask;
        }
    }
}
