namespace CartService.Bll;

public sealed class CartItem
{
    public CartItem(Guid id, string name, CartItemImage? image, decimal price, int quantity)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Item id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Item name is required.", nameof(name));
        }

        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Item price must be positive.");
        }

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        Id = id;
        Name = name.Trim();
        Image = image;
        Price = price;
        Quantity = quantity;
    }

    public Guid Id { get; }

    public string Name { get; }

    public CartItemImage? Image { get; }

    public decimal Price { get; }

    public int Quantity { get; private set; }

    public CartItem Copy()
    {
        return new CartItem(Id, Name, Image?.Copy(), Price, Quantity);
    }

    public void IncreaseQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        }

        Quantity += quantity;
    }
}