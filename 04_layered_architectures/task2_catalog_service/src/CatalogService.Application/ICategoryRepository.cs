using CatalogService.Domain;

namespace CatalogService.Application;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default);

    Task AddAsync(Category category, CancellationToken cancellationToken = default);

    Task<bool> UpdateAsync(Category category, CancellationToken cancellationToken = default);

    Task<bool> DeleteWithProductsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}