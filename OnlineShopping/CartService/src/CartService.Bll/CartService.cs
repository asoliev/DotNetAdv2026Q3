namespace CartService.Bll;

public sealed class CartService(ICartRepository cartRepository)
{
    private readonly ICartRepository _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));

    public async Task<IReadOnlyList<CartItem>> GetItemsAsync(string cartKey, CancellationToken cancellationToken = default)
    {
        ValidateCartKey(cartKey);

        Cart? cart = await _cartRepository.GetByIdAsync(cartKey, cancellationToken).ConfigureAwait(false);
        return cart?.GetItems() ?? Array.Empty<CartItem>();
    }

    public async Task<IReadOnlyList<CartItem>> AddItemAsync(string cartKey, CartItem item, CancellationToken cancellationToken = default)
    {
        ValidateCartKey(cartKey);
        ArgumentNullException.ThrowIfNull(item);

        Cart cart = await LoadOrCreateAsync(cartKey, cancellationToken).ConfigureAwait(false);
        cart.AddItem(item);
        await _cartRepository.UpsertAsync(cart, cancellationToken).ConfigureAwait(false);

        return cart.GetItems();
    }

    public async Task<bool> RemoveItemAsync(string cartKey, Guid itemId, CancellationToken cancellationToken = default)
    {
        ValidateCartKey(cartKey);

        Cart? cart = await _cartRepository.GetByIdAsync(cartKey, cancellationToken).ConfigureAwait(false);
        if (cart is null)
        {
            return false;
        }

        var removed = cart.RemoveItem(itemId);
        if (!removed)
        {
            return false;
        }

        await _cartRepository.UpsertAsync(cart, cancellationToken).ConfigureAwait(false);

        return true;
    }

    public async Task UpdateCatalogItemAsync(Guid itemId, string name, CartItemImage? image, decimal price, CancellationToken cancellationToken = default)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(itemId), "Item id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Item name is required.", nameof(name));
        }

        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Item price must be positive.");
        }

        IReadOnlyList<Cart> carts = await _cartRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (Cart cart in carts)
        {
            if (!cart.UpdateItem(itemId, name, image, price))
            {
                continue;
            }

            await _cartRepository.UpsertAsync(cart, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RemoveCatalogItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(itemId), "Item id must not be empty.");
        }

        IReadOnlyList<Cart> carts = await _cartRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        foreach (Cart cart in carts)
        {
            if (!cart.RemoveItem(itemId))
            {
                continue;
            }

            await _cartRepository.UpsertAsync(cart, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<Cart> LoadOrCreateAsync(string cartKey, CancellationToken cancellationToken)
    {
        Cart? existingCart = await _cartRepository.GetByIdAsync(cartKey, cancellationToken).ConfigureAwait(false);
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
