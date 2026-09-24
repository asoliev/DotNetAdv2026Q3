using CatalogService.Domain;

namespace CatalogService.Application;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<Product>> GetPageAsync(Guid? categoryId, int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);

    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task DeleteByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default);
}
