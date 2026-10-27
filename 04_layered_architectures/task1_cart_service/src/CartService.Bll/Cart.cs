namespace CartService.Bll;

public sealed class Cart
{
    private readonly List<CartItem> _items = new();

    public Cart(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Cart id must not be empty.", nameof(id));
        }

        Id = id;
    }

    public Cart(Guid id, IEnumerable<CartItem> items)
        : this(id)
    {
        foreach (var item in items)
        {
            AddItem(item);
        }
    }

    public Guid Id { get; }

    public IReadOnlyList<CartItem> GetItems()
    {
        return _items.Select(item => item.Copy()).ToList();
    }

    public void AddItem(CartItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var existingItem = _items.FirstOrDefault(currentItem => currentItem.Id == item.Id);

        if (existingItem is null)
        {
            _items.Add(item.Copy());
            return;
        }

        existingItem.IncreaseQuantity(item.Quantity);
    }

    public bool RemoveItem(int itemId)
    {
        var existingItem = _items.FirstOrDefault(item => item.Id == itemId);

        if (existingItem is null)
        {
            return false;
        }

        _items.Remove(existingItem);
        return true;
    }
}