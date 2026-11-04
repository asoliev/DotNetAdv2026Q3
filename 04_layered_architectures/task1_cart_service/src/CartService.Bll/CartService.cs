namespace CartService.Bll;

public sealed class CartService
{
    private readonly ICartRepository _cartRepository;

    public CartService(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
    }

    public async Task<IReadOnlyList<CartItem>> GetItemsAsync(string cartKey, CancellationToken cancellationToken = default)
    {
        ValidateCartKey(cartKey);

        var cart = await _cartRepository.GetByIdAsync(cartKey, cancellationToken);
        return cart?.GetItems() ?? Array.Empty<CartItem>();
    }

    public async Task<IReadOnlyList<CartItem>> AddItemAsync(string cartKey, CartItem item, CancellationToken cancellationToken = default)
    {
        ValidateCartKey(cartKey);
        ArgumentNullException.ThrowIfNull(item);

        var cart = await LoadOrCreateAsync(cartKey, cancellationToken);
        cart.AddItem(item);
        await _cartRepository.UpsertAsync(cart, cancellationToken);

        return cart.GetItems();
    }

    public async Task<bool> RemoveItemAsync(string cartKey, int itemId, CancellationToken cancellationToken = default)
    {
        ValidateCartKey(cartKey);

        var cart = await _cartRepository.GetByIdAsync(cartKey, cancellationToken);
        if (cart is null)
        {
            return false;
        }

        var removed = cart.RemoveItem(itemId);
        if (!removed)
        {
            return false;
        }

        await _cartRepository.UpsertAsync(cart, cancellationToken);

        return true;
    }

    private async Task<Cart> LoadOrCreateAsync(string cartKey, CancellationToken cancellationToken)
    {
        var existingCart = await _cartRepository.GetByIdAsync(cartKey, cancellationToken);
        return existingCart ?? new Cart(cartKey);
    }

    private static void ValidateCartKey(string cartKey)
    {
        if (string.IsNullOrWhiteSpace(cartKey))
        {
            throw new ArgumentException("Cart key must not be empty.", nameof(cartKey));
        }
    }
}