using CartService.Bll;

namespace CartService.Tests;

public class CartTests
{
    [Fact]
    public void AddItem_WhenItemAlreadyExists_IncreasesQuantity()
    {
        var cart = new Cart(Guid.NewGuid());
        cart.AddItem(new CartItem(1, "Keyboard", null, 99.99m, 1));

        cart.AddItem(new CartItem(1, "Keyboard", null, 99.99m, 2));

        var item = Assert.Single(cart.GetItems());
        Assert.Equal(3, item.Quantity);
    }

    [Fact]
    public void RemoveItem_WhenItemExists_RemovesItFromCart()
    {
        var cart = new Cart(Guid.NewGuid());
        cart.AddItem(new CartItem(1, "Keyboard", null, 99.99m, 1));

        var removed = cart.RemoveItem(1);

        Assert.True(removed);
        Assert.Empty(cart.GetItems());
    }
}