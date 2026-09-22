using CatalogService.Domain;

namespace CatalogService.Tests;

public class InfrastructureIntegrationTests
{
    [Fact]
    public async Task CategoryRepository_PersistsAndLoadsCategory()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"catalog-{Guid.NewGuid():N}.db");

        try
        {
            var categoryId = Guid.NewGuid();
            var parentId = Guid.NewGuid();

            var parentRepository = new global::CatalogService.Infrastructure.SqliteCategoryRepository(databasePath);
            await parentRepository.AddAsync(new Category(parentId, "Electronics"));

            var repository = new global::CatalogService.Infrastructure.SqliteCategoryRepository(databasePath);
            var category = new Category(categoryId, "Accessories", new ImageInfo("https://example.com/accessories.png", "Accessories"), parentId);

            await repository.AddAsync(category);

            var loaded = await repository.GetByIdAsync(categoryId);

            Assert.NotNull(loaded);
            Assert.Equal(categoryId, loaded!.Id);
            Assert.Equal("Accessories", loaded.Name);
            Assert.Equal(parentId, loaded.ParentCategoryId);
            Assert.NotNull(loaded.Image);
            Assert.Equal("https://example.com/accessories.png", loaded.Image!.Url);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task ProductRepository_PersistsAndLoadsProduct()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"catalog-{Guid.NewGuid():N}.db");

        try
        {
            var categoryId = Guid.NewGuid();
            var productId = Guid.NewGuid();

            var categoryRepository = new global::CatalogService.Infrastructure.SqliteCategoryRepository(databasePath);
            await categoryRepository.AddAsync(new Category(categoryId, "Phones"));

            var repository = new global::CatalogService.Infrastructure.SqliteProductRepository(databasePath);
            var product = new Product(productId, "Smartphone", "<p>Android phone</p>", new ImageInfo("https://example.com/phone.png", "Phone"), categoryId, 499.99m, 5);

            await repository.AddAsync(product);

            var loaded = await repository.GetByIdAsync(productId);

            Assert.NotNull(loaded);
            Assert.Equal(productId, loaded!.Id);
            Assert.Equal(categoryId, loaded.CategoryId);
            Assert.Equal(499.99m, loaded.Price);
            Assert.Equal(5, loaded.Amount);
            Assert.NotNull(loaded.Image);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}