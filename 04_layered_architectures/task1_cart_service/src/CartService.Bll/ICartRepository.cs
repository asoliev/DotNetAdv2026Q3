namespace CartService.Bll;

public interface ICartRepository
{
    Task<Cart?> GetByIdAsync(Guid cartId, CancellationToken cancellationToken = default);

    Task UpsertAsync(Cart cart, CancellationToken cancellationToken = default);
}