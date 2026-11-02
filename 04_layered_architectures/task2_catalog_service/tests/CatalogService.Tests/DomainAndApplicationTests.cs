using CatalogService.Application;
using CatalogService.Domain;

namespace CatalogService.Tests;

public class DomainAndApplicationTests
{
    [Fact]
    public void Category_NameLongerThanFiftyCharacters_Throws()
    {
        var name = new string('a', 51);

        var exception = Assert.Throws<ArgumentException>(() => new Category(Guid.NewGuid(), name));

        Assert.Contains("50 characters", exception.Message);
    }

    [Fact]
    public async Task ProductService_AddAsync_WithMissingCategory_Throws()
    {
        var productRepository = new FakeProductRepository();
        var categoryRepository = new FakeCategoryRepository();
        var service = new ProductService(productRepository, categoryRepository);

        var product = new Product(Guid.NewGuid(), "Phone", null, null, Guid.NewGuid(), 299.99m, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AddAsync(product));
    }

    [Fact]
    public async Task CategoryService_AddAsync_WithExistingParent_Succeeds()
    {
        var parentId = Guid.NewGuid();
        var categoryRepository = new FakeCategoryRepository(parentId);
        var service = new CategoryService(categoryRepository);

        var category = new Category(Guid.NewGuid(), "Accessories", null, parentId);

        await service.AddAsync(category);

        Assert.Single(categoryRepository.StoredCategories);
    }

    [Fact]
    public async Task ProductService_GetPageAsync_ReturnsFilteredPage()
    {
        var categoryId = Guid.NewGuid();
        var productRepository = new FakeProductRepository(
            new Product(Guid.NewGuid(), "Mouse", null, null, categoryId, 19.99m, 2),
            new Product(Guid.NewGuid(), "Keyboard", null, null, categoryId, 49.99m, 1),
            new Product(Guid.NewGuid(), "Monitor", null, null, Guid.NewGuid(), 199.99m, 1));
        var service = new ProductService(productRepository, new FakeCategoryRepository(categoryId));

        var page = await service.GetPageAsync(categoryId, 1, 1);

        Assert.Equal(2, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal("Keyboard", page.Items[0].Name);
    }

    [Fact]
    public async Task CategoryService_DeleteAsync_UsesAtomicRepositoryOperation()
    {
        var categoryId = Guid.NewGuid();
        var categoryRepository = new FakeCategoryRepository(categoryId);
        var service = new CategoryService(categoryRepository);

        var deleted = await service.DeleteAsync(categoryId);

        Assert.True(deleted);
        Assert.Equal(categoryId, categoryRepository.DeletedCategoryIds.Single());
    }

    [Fact]
    public async Task CategoryService_DeleteAsync_WhenCategoryHasChildren_ThrowsConflict()
    {
        var categoryId = Guid.NewGuid();
        var categoryRepository = new FakeCategoryRepository(categoryId);
        categoryRepository.CategoriesWithChildren.Add(categoryId);
        var service = new CategoryService(categoryRepository);

        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteAsync(categoryId));
        Assert.Empty(categoryRepository.DeletedCategoryIds);
    }

    [Fact]
    public async Task CategoryService_UpdateAsync_WhenCategoryIsMissing_ReturnsFalse()
    {
        var categoryRepository = new FakeCategoryRepository();
        var service = new CategoryService(categoryRepository);
        var category = new Category(Guid.NewGuid(), "Missing");

        var updated = await service.UpdateAsync(category);

        Assert.False(updated);
    }

    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        private readonly HashSet<Guid> _existingCategories = new();
        public HashSet<Guid> CategoriesWithChildren { get; } = new();

        public FakeCategoryRepository(params Guid[] existingCategoryIds)
        {
            foreach (var id in existingCategoryIds)
            {
                _existingCategories.Add(id);
            }
        }

        public List<Category> StoredCategories { get; } = new();

        public List<Guid> DeletedCategoryIds { get; } = new();

        public Task<Category?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Category?>(StoredCategories.FirstOrDefault(category => category.Id == id));
        }

        public Task<IReadOnlyList<Category>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Category>>(StoredCategories.ToList());
        }

        public Task AddAsync(Category category, CancellationToken cancellationToken = default)
        {
            StoredCategories.Add(category);
            _existingCategories.Add(category.Id);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(Category category, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_existingCategories.Contains(category.Id));
        }

        public Task<bool> DeleteWithProductsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            DeletedCategoryIds.Add(id);
            return Task.FromResult(_existingCategories.Remove(id));
        }

        public Task<bool> HasChildrenAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CategoriesWithChildren.Contains(id));
        }

        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_existingCategories.Contains(id));
        }
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly List<Product> _products = new();

        public FakeProductRepository(params Product[] products)
        {
            _products.AddRange(products);
        }

        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Product?>(_products.FirstOrDefault(product => product.Id == id));
        }

        public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Product>>(_products.ToList());
        }

        public Task<PagedResult<Product>> GetPageAsync(Guid? categoryId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _products.AsEnumerable();
            if (categoryId is not null)
            {
                query = query.Where(product => product.CategoryId == categoryId.Value);
            }

            var filtered = query.OrderBy(product => product.Name).ToList();
            var pageItems = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedResult<Product>(pageItems, filtered.Count, pageNumber, pageSize));
        }

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            _products.Add(product);
            return Task.CompletedTask;
        }

        public Task<bool> UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            var index = _products.FindIndex(existing => existing.Id == product.Id);
            if (index < 0)
            {
                return Task.FromResult(false);
            }

            _products[index] = product;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_products.RemoveAll(product => product.Id == id) > 0);
        }
    }
}