using CatalogService.Domain;

namespace CatalogService.Application;

public sealed class ProductService(IProductRepository productRepository, ICategoryRepository categoryRepository)
{
    private readonly IProductRepository _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
    private readonly ICategoryRepository _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _productRepository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default) => _productRepository.GetAllAsync(cancellationToken);

    public Task<PagedResult<Product>> GetPageAsync(Guid? categoryId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        if (pageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Page number must be positive.");
        }

        if (pageSize <= 0 || pageSize > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), "Page size must be between 1 and 100.");
        }

        return _productRepository.GetPageAsync(categoryId, pageNumber, pageSize, cancellationToken);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        await EnsureCategoryExistsAsync(product.CategoryId, cancellationToken).ConfigureAwait(false);
        await _productRepository.AddAsync(product, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        await EnsureCategoryExistsAsync(product.CategoryId, cancellationToken).ConfigureAwait(false);
        await _productRepository.UpdateAsync(product, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => _productRepository.DeleteAsync(id, cancellationToken);

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (!await _categoryRepository.ExistsAsync(categoryId, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Product category does not exist.");
        }
    }
}
