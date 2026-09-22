using CatalogService.Domain;

namespace CatalogService.Application;

public sealed class ProductService
{
    private readonly IProductRepository _productRepository;
    private readonly ICategoryRepository _categoryRepository;

    public ProductService(IProductRepository productRepository, ICategoryRepository categoryRepository)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
    }

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _productRepository.GetByIdAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _productRepository.GetAllAsync(cancellationToken);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        await EnsureCategoryExistsAsync(product.CategoryId, cancellationToken);
        await _productRepository.AddAsync(product, cancellationToken);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(product);
        await EnsureCategoryExistsAsync(product.CategoryId, cancellationToken);
        await _productRepository.UpdateAsync(product, cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _productRepository.DeleteAsync(id, cancellationToken);
    }

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (!await _categoryRepository.ExistsAsync(categoryId, cancellationToken))
        {
            throw new InvalidOperationException("Product category does not exist.");
        }
    }
}