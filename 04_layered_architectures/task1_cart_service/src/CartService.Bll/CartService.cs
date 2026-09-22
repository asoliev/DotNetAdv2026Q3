namespace CartService.Bll;

public sealed class CartService
{
    private readonly ICartRepository _cartRepository;

    public CartService(ICartRepository cartRepository)
    {
        _cartRepository = cartRepository ?? throw new ArgumentNullException(nameof(cartRepository));
    }

    public async Task<IReadOnlyList<CartItem>> GetItemsAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        ValidateCartId(cartId);

        var cart = await _cartRepository.GetByIdAsync(cartId, cancellationToken);
        return cart?.GetItems() ?? Array.Empty<CartItem>();
    }

    public async Task<IReadOnlyList<CartItem>> AddItemAsync(Guid cartId, CartItem item, CancellationToken cancellationToken = default)
    {
        ValidateCartId(cartId);
        ArgumentNullException.ThrowIfNull(item);

        var cart = await LoadOrCreateAsync(cartId, cancellationToken);
        cart.AddItem(item);
        await _cartRepository.UpsertAsync(cart, cancellationToken);

        return cart.GetItems();
    }

    public async Task<IReadOnlyList<CartItem>> RemoveItemAsync(Guid cartId, int itemId, CancellationToken cancellationToken = default)
    {
        ValidateCartId(cartId);

        var cart = await _cartRepository.GetByIdAsync(cartId, cancellationToken);
        if (cart is null)
        {
            return Array.Empty<CartItem>();
        }

        cart.RemoveItem(itemId);
        await _cartRepository.UpsertAsync(cart, cancellationToken);

        return cart.GetItems();
    }

    private async Task<Cart> LoadOrCreateAsync(Guid cartId, CancellationToken cancellationToken)
    {
        var existingCart = await _cartRepository.GetByIdAsync(cartId, cancellationToken);
        return existingCart ?? new Cart(cartId);
    }

    private static void ValidateCartId(Guid cartId)
    {
        if (cartId == Guid.Empty)
        {
            throw new ArgumentException("Cart id must not be empty.", nameof(cartId));
        }
    }
}