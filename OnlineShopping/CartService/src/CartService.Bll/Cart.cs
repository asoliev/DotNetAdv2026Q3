namespace CartService.Bll;

public sealed class Cart
{
    private readonly List<CartItem> _items = new();

    public Cart(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Cart key must not be empty.", nameof(key));
        }

        Id = key.Trim();
    }

    public Cart(string key, IEnumerable<CartItem> items)
        : this(key)
    {
        foreach (CartItem item in items)
        {
            AddItem(item);
        }
    }

    public string Id { get; }

    public IReadOnlyList<CartItem> GetItems() => _items.Select(item => item.Copy()).ToList();

    public void AddItem(CartItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        CartItem? existingItem = _items.FirstOrDefault(currentItem => currentItem.Id == item.Id);

        if (existingItem is null)
        {
            _items.Add(item.Copy());
            return;
        }

        existingItem.IncreaseQuantity(item.Quantity);
    }

    public bool RemoveItem(Guid itemId)
    {
        CartItem? existingItem = _items.FirstOrDefault(item => item.Id == itemId);

        if (existingItem is null)
        {
            return false;
        }

        _items.Remove(existingItem);
        return true;
    }

    public bool UpdateItem(Guid itemId, string name, CartItemImage? image, decimal price)
    {
        CartItem? existingItem = _items.FirstOrDefault(item => item.Id == itemId);

        if (existingItem is null)
        {
            return false;
        }

        var quantity = existingItem.Quantity;
        var existingIndex = _items.IndexOf(existingItem);
        _items[existingIndex] = new CartItem(itemId, name, image, price, quantity);
        return true;
    }
}
