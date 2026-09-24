namespace CartService.Bll;

public interface ICartRepository
{
    Task<Cart?> GetByIdAsync(string cartKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Cart>> GetAllAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(Cart cart, CancellationToken cancellationToken = default);
}
