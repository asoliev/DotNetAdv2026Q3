namespace CartService.Bll;

public sealed class CartManager
{
    private readonly ICartRepository _cartRepository;

    public CartManager(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
    }

    public Task<IReadOnlyList<CartItem>> GetItemsAsync(string cartKey, CancellationToken cancellationToken = default)
    {
        ValidateCartKey(cartKey);

        return GetItemsCoreAsync(cartKey, cancellationToken);
    }

    public Task<IReadOnlyList<CartItem>> AddItemAsync(string cartKey, CartItem item, CancellationToken cancellationToken = default)
    {
        ValidateCartKey(cartKey);
        ArgumentNullException.ThrowIfNull(item);

        return AddItemCoreAsync(cartKey, item, cancellationToken);
    }

    public Task<bool> RemoveItemAsync(string cartKey, Guid itemId, CancellationToken cancellationToken = default)
    {
        ValidateCartKey(cartKey);

        return RemoveItemCoreAsync(cartKey, itemId, cancellationToken);
    }

    public Task UpdateCatalogItemAsync(Guid itemId, string name, CartItemImage? image, decimal price, CancellationToken cancellationToken = default)
    {
        ValidateCatalogItem(itemId, name, price);

        return UpdateCatalogItemCoreAsync(itemId, name, image, price, cancellationToken);
    }

    public Task RemoveCatalogItemAsync(Guid itemId, CancellationToken cancellationToken = default)
    {
        ValidateCatalogItemId(itemId);

        return RemoveCatalogItemCoreAsync(itemId, cancellationToken);
    }

    private async Task<IReadOnlyList<CartItem>> GetItemsCoreAsync(string cartKey, CancellationToken cancellationToken)
    {
        Cart? cart = await _cartRepository.GetByIdAsync(cartKey, cancellationToken).ConfigureAwait(false);
        return cart?.GetItems() ?? Array.Empty<CartItem>();
    }

    private async Task<IReadOnlyList<CartItem>> AddItemCoreAsync(string cartKey, CartItem item, CancellationToken cancellationToken)
    {
        Cart cart = await LoadOrCreateAsync(cartKey, cancellationToken).ConfigureAwait(false);
        cart.AddItem(item);
        await _cartRepository.UpsertAsync(cart, cancellationToken).ConfigureAwait(false);

        return cart.GetItems();
    }

    private async Task<bool> RemoveItemCoreAsync(string cartKey, Guid itemId, CancellationToken cancellationToken)
    {
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

    private async Task UpdateCatalogItemCoreAsync(Guid itemId, string name, CartItemImage? image, decimal price, CancellationToken cancellationToken)
    {
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

    private async Task RemoveCatalogItemCoreAsync(Guid itemId, CancellationToken cancellationToken)
    {
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

    private static void ValidateCatalogItem(Guid itemId, string name, decimal price)
    {
        ValidateCatalogItemId(itemId);

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Item name is required.", nameof(name));
        }

        if (price <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Item price must be positive.");
        }
    }

    private static void ValidateCatalogItemId(Guid itemId)
    {
        if (itemId == Guid.Empty)
        {
            throw new ArgumentOutOfRangeException(nameof(itemId), "Item id must not be empty.");
        }
    }
}
