using CatalogService.Domain;

namespace CatalogService.Application;

public sealed class CategoryService
{
    private readonly ICategoryRepository _categoryRepository;

    public CategoryService(ICategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository ?? throw new ArgumentNullException(nameof(categoryRepository));
    }

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _categoryRepository.GetByIdAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _categoryRepository.GetAllAsync(cancellationToken);
    }

    public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        await ValidateParentCategoryAsync(category, cancellationToken);
        await _categoryRepository.AddAsync(category, cancellationToken);
    }

    public async Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(category);
        await ValidateParentCategoryAsync(category, cancellationToken);
        await _categoryRepository.UpdateAsync(category, cancellationToken);
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _categoryRepository.DeleteAsync(id, cancellationToken);
    }

    private async Task ValidateParentCategoryAsync(Category category, CancellationToken cancellationToken)
    {
        if (category.ParentCategoryId is null)
        {
            return;
        }

        if (!await _categoryRepository.ExistsAsync(category.ParentCategoryId.Value, cancellationToken))
        {
            throw new InvalidOperationException("Parent category does not exist.");
        }
    }
}