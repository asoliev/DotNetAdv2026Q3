using CatalogService.Domain;

namespace CatalogService.Application;

public sealed class CategoryService(ICategoryRepository categoryRepository, IProductRepository productRepository)
{
    private readonly ICategoryRepository _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
    private readonly IProductRepository _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => _categoryRepository.GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default) => _categoryRepository.GetAllAsync(cancellationToken);

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        await ValidateParentCategoryAsync(category, cancellationToken).ConfigureAwait(false);
        await _categoryRepository.AddAsync(category, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        await ValidateParentCategoryAsync(category, cancellationToken).ConfigureAwait(false);
        await _categoryRepository.UpdateAsync(category, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => DeleteWithProductsAsync(id, cancellationToken);

    private async Task DeleteWithProductsAsync(Guid id, CancellationToken cancellationToken)
    {
        await _productRepository.DeleteByCategoryIdAsync(id, cancellationToken).ConfigureAwait(false);
        await _categoryRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
    }

    private async Task ValidateParentCategoryAsync(Category category, CancellationToken cancellationToken)
    {
        if (category.ParentCategoryId is null)
        {
            return;
        }

        if (!await _categoryRepository.ExistsAsync(category.ParentCategoryId.Value, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("Parent category does not exist.");
        }
    }
}
