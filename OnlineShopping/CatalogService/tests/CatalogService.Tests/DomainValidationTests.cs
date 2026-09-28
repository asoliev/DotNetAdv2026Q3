using CatalogService.Application;
using CatalogService.Domain;
using CatalogService.Infrastructure;

using Microsoft.Data.Sqlite;

namespace CatalogService.Tests;

public class DomainValidationTests
{
    private static readonly Guid CategoryId = Guid.NewGuid();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CategoryAndProductRequireAName(string name)
    {
        Assert.Throws<ArgumentException>(() => new Category(Guid.NewGuid(), name));
        Assert.Throws<ArgumentException>(() => new Product(Guid.NewGuid(), name, null, null, CategoryId, 1m, 1));
    }

    [Fact]
    public void ProductNameLongerThanFiftyCharactersThrows() =>
        Assert.Throws<ArgumentException>(() => new Product(Guid.NewGuid(), new string('a', 51), null, null, CategoryId, 1m, 1));

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    public void ProductPriceAndAmountMustBePositive(int price, int amount) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Product(Guid.NewGuid(), "Phone", null, null, CategoryId, price, amount));

    [Fact]
    public void ProductRequiresACategory() =>
        Assert.Throws<ArgumentException>(() => new Product(Guid.NewGuid(), "Phone", null, null, Guid.Empty, 1m, 1));

    [Fact]
    public void EmptyIdsAreReplacedAndTextIsNormalized()
    {
        var category = new Category(Guid.Empty, "  Books  ");
        var product = new Product(Guid.Empty, " Phone ", "   ", null, CategoryId, 1m, 1);

        Assert.NotEqual(Guid.Empty, category.Id);
        Assert.Equal("Books", category.Name);
        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal("Phone", product.Name);
        Assert.Null(product.Description);
    }

    [Fact]
    public void UpdateReplacesValuesAndValidates()
    {
        var parentId = Guid.NewGuid();
        var image = new ImageInfo("https://example.com/a.png", "  Alt  ");
        var category = new Category(Guid.NewGuid(), "Books");
        var product = new Product(Guid.NewGuid(), "Phone", null, null, CategoryId, 1m, 1);

        category.Update(" E-books ", image, parentId);
        product.Update(" Phone 2 ", "<p>New</p>", image, parentId, 2m, 3);

        Assert.Equal(("E-books", image, parentId), (category.Name, category.Image, category.ParentCategoryId));
        Assert.Equal(("Phone 2", "<p>New</p>", image, parentId, 2m, 3), (product.Name, product.Description, product.Image, product.CategoryId, product.Price, product.Amount));
        Assert.Equal("Alt", image.AltText);
        Assert.Throws<ArgumentException>(() => category.Update(string.Empty, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => product.Update("Phone", null, null, CategoryId, 0m, 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("images/relative.png")]
    public void ImageUrlMustBeAbsolute(string url) => Assert.Throws<ArgumentException>(() => new ImageInfo(url, null));

    [Fact]
    public void BlankImageAltTextBecomesNull() => Assert.Null(new ImageInfo("https://example.com/a.png", " ").AltText);

    [Fact]
    public async Task ServicesReadThroughToRepositoriesAndValidatePaging()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"catalog-{Guid.NewGuid():N}.db");
        try
        {
            var categoryRepository = new SqliteCategoryRepository(databasePath);
            var productRepository = new SqliteProductRepository(databasePath);
            var categoryService = new CategoryService(categoryRepository, productRepository);
            var productService = new ProductService(productRepository, categoryRepository);
            var category = new Category(Guid.NewGuid(), "Phones");
            var product = new Product(Guid.NewGuid(), "Phone", null, null, category.Id, 1m, 1);

            await categoryService.AddAsync(category, TestContext.Current.CancellationToken);
            await productService.AddAsync(product, TestContext.Current.CancellationToken);

            Assert.Equal(category.Id, (await categoryService.GetByIdAsync(category.Id, TestContext.Current.CancellationToken))?.Id);
            Assert.Equal(category.Id, Assert.Single(await categoryService.GetAllAsync(TestContext.Current.CancellationToken)).Id);
            Assert.Equal(product.Id, (await productService.GetByIdAsync(product.Id, TestContext.Current.CancellationToken))?.Id);
            Assert.Equal(product.Id, Assert.Single(await productService.GetAllAsync(TestContext.Current.CancellationToken)).Id);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => productService.GetPageAsync(null, 0, 10, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => productService.GetPageAsync(null, 1, 101, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentNullException>(() => categoryService.AddAsync(null!, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentNullException>(() => productService.UpdateAsync(null!, TestContext.Current.CancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }
}
