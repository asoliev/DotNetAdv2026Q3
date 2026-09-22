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

    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        private readonly HashSet<Guid> _existingCategories = new();

        public FakeCategoryRepository(params Guid[] existingCategoryIds)
        {
            foreach (var id in existingCategoryIds)
            {
                _existingCategories.Add(id);
            }
        }

        public List<Category> StoredCategories { get; } = new();

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

        public Task UpdateAsync(Category category, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_existingCategories.Contains(id));
        }
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Product?>(null);
        }

        public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Product>>(Array.Empty<Product>());
        }

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}