using CartService.Bll;

namespace CartService.Tests;

public class CartTests
{
    [Fact]
    public void AddItem_WhenItemAlreadyExists_IncreasesQuantity()
    {
        var cart = new Cart(Guid.NewGuid().ToString("N"));
        cart.AddItem(new CartItem(Guid.NewGuid(), "Keyboard", null, 99.99m, 1));

        cart.AddItem(new CartItem(cart.GetItems().Single().Id, "Keyboard", null, 99.99m, 2));

        var item = Assert.Single(cart.GetItems());
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public void RemoveItem_WhenItemExists_RemovesItFromCart()
    {
        var cart = new Cart(Guid.NewGuid().ToString("N"));
        var itemId = Guid.NewGuid();
        cart.AddItem(new CartItem(itemId, "Keyboard", null, 99.99m, 1));

        var removed = cart.RemoveItem(itemId);

        Assert.True(removed);
        Assert.Empty(cart.GetItems());
    }

    [Fact]
    public void UpdateItem_WhenItemExists_UpdatesProductDetailsAndKeepsQuantity()
    {
        var cart = new Cart(Guid.NewGuid().ToString("N"));
        var itemId = Guid.NewGuid();
        cart.AddItem(new CartItem(itemId, "Keyboard", null, 99.99m, 1));

        var updated = cart.UpdateItem(itemId, "Mechanical Keyboard", new CartItemImage("https://example.com/keyboard.png", "Keyboard"), 129.99m);

        Assert.True(updated);
        var item = Assert.Single(cart.GetItems());
        Assert.Equal("Mechanical Keyboard", item.Name);
        Assert.Equal(129.99m, item.Price);
        Assert.Equal(1, item.Quantity);
    }
}