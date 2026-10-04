using System.Net;
using System.Net.Http.Json;

using CatalogService.Api;

namespace CatalogService.Tests.Api;

public sealed class CatalogApiTests(CatalogApiFactory factory) : IClassFixture<CatalogApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SwaggerDocumentIsServed()
    {
        using HttpClient client = factory.CreateClient(accessToken: null);

        var payload = await client.GetStringAsync(Relative("/swagger/v1/swagger.json"), Ct);

        Assert.Contains("Catalog Service API", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadEndpointsAreAnonymousAndReportSupportedVersions()
    {
        using HttpClient client = factory.CreateClient(accessToken: null);

        using HttpResponseMessage response = await client.GetAsync(Relative("api/v1/categories"), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("1.0", Assert.Single(response.Headers.GetValues("api-supported-versions")));
    }

    [Fact]
    public async Task UnsupportedApiVersionReturnsNotFound()
    {
        using HttpClient client = factory.CreateClient(accessToken: null);

        using HttpResponseMessage response = await client.GetAsync(Relative("api/v9/categories"), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WriteEndpointsRejectAnonymousInvalidAndNonManagerCallers()
    {
        var category = new CategoryUpsertRequest("Denied", null, null);
        var product = new ProductUpsertRequest("Denied", null, null, Guid.NewGuid(), 1m, 1);

        using HttpClient anonymous = factory.CreateClient(accessToken: null);
        using HttpClient forged = factory.CreateClient(TestTokens.Create(ShoppingAuth.AuthRoles.Manager, "a-different-signing-key-that-is-long-enough"));
        using HttpClient customer = factory.CreateClient(TestTokens.StoreCustomer);

        foreach ((HttpClient client, HttpStatusCode expected) in new[] { (anonymous, HttpStatusCode.Unauthorized), (forged, HttpStatusCode.Unauthorized), (customer, HttpStatusCode.Forbidden) })
        {
            using HttpResponseMessage createCategory = await client.PostAsJsonAsync(Relative("api/v1/categories"), category, Ct);
            using HttpResponseMessage updateCategory = await client.PutAsJsonAsync(Relative($"api/v1/categories/{Guid.NewGuid()}"), category, Ct);
            using HttpResponseMessage deleteCategory = await client.DeleteAsync(Relative($"api/v1/categories/{Guid.NewGuid()}"), Ct);
            using HttpResponseMessage createProduct = await client.PostAsJsonAsync(Relative("api/v1/products"), product, Ct);
            using HttpResponseMessage updateProduct = await client.PutAsJsonAsync(Relative($"api/v1/products/{Guid.NewGuid()}"), product, Ct);
            using HttpResponseMessage deleteProduct = await client.DeleteAsync(Relative($"api/v1/products/{Guid.NewGuid()}"), Ct);

            Assert.All([createCategory, updateCategory, deleteCategory, createProduct, updateProduct, deleteProduct], response => Assert.Equal(expected, response.StatusCode));
        }
    }

    [Fact]
    public async Task CategoryCanBeCreatedReadUpdatedAndDeleted()
    {
        using HttpClient client = factory.CreateClient(TestTokens.Manager);

        using HttpResponseMessage created = await client.PostAsJsonAsync(Relative("api/v1/categories"), new CategoryUpsertRequest("Books", new ImageRequest("https://example.com/books.png", "Books"), null), Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(created.Headers.Location);
        CategoryResponse category = await ReadAsync<CategoryResponse>(created);
        Assert.Equal("Books", category.Name);
        Assert.Equal("https://example.com/books.png", category.Image?.Url);

        CategoryResponse loaded = await GetAsync<CategoryResponse>(client, $"api/v1/categories/{category.Id}");
        Assert.Equal(category, loaded);

        using HttpResponseMessage updated = await client.PutAsJsonAsync(Relative($"api/v1/categories/{category.Id}"), new CategoryUpsertRequest("E-books", null, null), Ct);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        CategoryResponse reloaded = await GetAsync<CategoryResponse>(client, $"api/v1/categories/{category.Id}");
        Assert.Equal("E-books", reloaded.Name);
        Assert.Null(reloaded.Image);

        List<CategoryResponse> all = await GetAsync<List<CategoryResponse>>(client, "api/v1/categories");
        Assert.Contains(all, item => item.Id == category.Id);

        using HttpResponseMessage deleted = await client.DeleteAsync(Relative($"api/v1/categories/{category.Id}"), Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await AssertStatusAsync(client, HttpMethod.Get, $"api/v1/categories/{category.Id}", HttpStatusCode.NotFound);
        await AssertStatusAsync(client, HttpMethod.Delete, $"api/v1/categories/{category.Id}", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ChildCategoryRequiresAnExistingParent()
    {
        using HttpClient client = factory.CreateClient(TestTokens.Manager);
        CategoryResponse parent = await CreateCategoryAsync(client, "Parent");

        using HttpResponseMessage child = await client.PostAsJsonAsync(Relative("api/v1/categories"), new CategoryUpsertRequest("Child", null, parent.Id), Ct);
        using HttpResponseMessage orphan = await client.PostAsJsonAsync(Relative("api/v1/categories"), new CategoryUpsertRequest("Orphan", null, Guid.NewGuid()), Ct);
        using HttpResponseMessage orphanUpdate = await client.PutAsJsonAsync(Relative($"api/v1/categories/{parent.Id}"), new CategoryUpsertRequest("Parent", null, Guid.NewGuid()), Ct);

        Assert.Equal(HttpStatusCode.Created, child.StatusCode);
        Assert.Equal(parent.Id, (await ReadAsync<CategoryResponse>(child)).ParentCategoryId);
        Assert.Equal(HttpStatusCode.BadRequest, orphan.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, orphanUpdate.StatusCode);
    }

    [Fact]
    public async Task MissingBodyOrUnknownIdsAreRejected()
    {
        using HttpClient client = factory.CreateClient(TestTokens.Manager);
        using var empty = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        using HttpResponseMessage noBody = await client.PostAsync(Relative("api/v1/categories"), empty, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, noBody.StatusCode);
        await AssertStatusAsync(client, HttpMethod.Get, $"api/v1/products/{Guid.NewGuid()}", HttpStatusCode.NotFound);
        await AssertStatusAsync(client, HttpMethod.Delete, $"api/v1/products/{Guid.NewGuid()}", HttpStatusCode.NotFound);
        await AssertStatusAsync(client, HttpMethod.Get, "api/v1/categories/not-a-guid", HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("""{ "name": "Phone", "price": 1.0, "amount": 1 }""")]
    [InlineData("""{ "name": "Phone", "categoryId": "7f8e0a52-8d2c-4a39-9b0e-2f8f3c1d4a10", "amount": 1 }""")]
    [InlineData("""{ "name": "Phone", "categoryId": "7f8e0a52-8d2c-4a39-9b0e-2f8f3c1d4a10", "price": 1.0 }""")]
    public async Task ProductWithMissingRequiredFieldIsRejected(string json)
    {
        using HttpClient client = factory.CreateClient(TestTokens.Manager);
        using var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(Relative("api/v1/products"), body, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProductCanBeCreatedReadUpdatedAndDeletedAndPublishesEvents()
    {
        using HttpClient client = factory.CreateClient(TestTokens.Manager);
        CategoryResponse category = await CreateCategoryAsync(client, "Phones");

        using HttpResponseMessage created = await client.PostAsJsonAsync(Relative("api/v1/products"), new ProductUpsertRequest("Phone", "<p>Android</p>", new ImageRequest("https://example.com/phone.png", "Phone"), category.Id, 499.99m, 5), Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        ProductResponse product = await ReadAsync<ProductResponse>(created);
        Assert.Contains(factory.Publisher.Upserted, message => message.Id == product.Id && message.Name == "Phone" && message.Image?.Url == "https://example.com/phone.png");

        ProductResponse loaded = await GetAsync<ProductResponse>(client, $"api/v1/products/{product.Id}");
        Assert.Equal(product, loaded);
        Assert.Equal(499.99m, loaded.Price);
        Assert.Equal(5, loaded.Amount);

        using HttpResponseMessage updated = await client.PutAsJsonAsync(Relative($"api/v1/products/{product.Id}"), new ProductUpsertRequest("Phone 2", null, null, category.Id, 399m, 3), Ct);
        Assert.Equal(HttpStatusCode.NoContent, updated.StatusCode);
        Assert.Contains(factory.Publisher.Upserted, message => message.Id == product.Id && message.Name == "Phone 2" && message.Image is null && message.Price == 399m);
        Assert.Equal("Phone 2", (await GetAsync<ProductResponse>(client, $"api/v1/products/{product.Id}")).Name);

        using HttpResponseMessage deleted = await client.DeleteAsync(Relative($"api/v1/products/{product.Id}"), Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Contains(product.Id, factory.Publisher.Deleted);
        await AssertStatusAsync(client, HttpMethod.Get, $"api/v1/products/{product.Id}", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ProductWithUnknownCategoryIsRejected()
    {
        using HttpClient client = factory.CreateClient(TestTokens.Manager);
        CategoryResponse category = await CreateCategoryAsync(client, "Tablets");
        ProductResponse product = await CreateProductAsync(client, "Tablet", category.Id);
        var unknownCategory = new ProductUpsertRequest("Tablet", null, null, Guid.NewGuid(), 1m, 1);

        using HttpResponseMessage create = await client.PostAsJsonAsync(Relative("api/v1/products"), unknownCategory, Ct);
        using HttpResponseMessage update = await client.PutAsJsonAsync(Relative($"api/v1/products/{product.Id}"), unknownCategory, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    [Fact]
    public async Task ProductsArePagedAndFilteredByCategory()
    {
        using HttpClient client = factory.CreateClient(TestTokens.Manager);
        CategoryResponse category = await CreateCategoryAsync(client, "Laptops");
        CategoryResponse other = await CreateCategoryAsync(client, "Monitors");
        foreach (var name in new[] { "Laptop A", "Laptop B", "Laptop C" })
        {
            await CreateProductAsync(client, name, category.Id);
        }

        await CreateProductAsync(client, "Monitor", other.Id);

        PageResponse<ProductResponse> firstPage = await GetAsync<PageResponse<ProductResponse>>(client, $"api/v1/products?categoryId={category.Id}&pageNumber=1&pageSize=2");
        PageResponse<ProductResponse> secondPage = await GetAsync<PageResponse<ProductResponse>>(client, $"api/v1/categories/{category.Id}/products?pageNumber=2&pageSize=2");
        PageResponse<ProductResponse> unfiltered = await GetAsync<PageResponse<ProductResponse>>(client, "api/v1/products");

        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(["Laptop A", "Laptop B"], firstPage.Items.Select(item => item.Name));
        Assert.Equal(2, secondPage.PageNumber);
        Assert.Equal("Laptop C", Assert.Single(secondPage.Items).Name);
        Assert.True(unfiltered.TotalCount >= 4);
        await AssertStatusAsync(client, HttpMethod.Get, $"api/v1/products?categoryId={Guid.NewGuid()}", HttpStatusCode.NotFound);
        await AssertStatusAsync(client, HttpMethod.Get, $"api/v1/categories/{Guid.NewGuid()}/products", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletingACategoryDeletesItsProducts()
    {
        using HttpClient client = factory.CreateClient(TestTokens.Manager);
        CategoryResponse category = await CreateCategoryAsync(client, "Cameras");
        ProductResponse product = await CreateProductAsync(client, "Camera", category.Id);

        using HttpResponseMessage deleted = await client.DeleteAsync(Relative($"api/v1/categories/{category.Id}"), Ct);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await AssertStatusAsync(client, HttpMethod.Get, $"api/v1/products/{product.Id}", HttpStatusCode.NotFound);
    }

    private static Uri Relative(string path) => new(path, UriKind.Relative);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<T>(Ct) ?? throw new InvalidOperationException("Empty response body.");

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        using HttpResponseMessage response = await client.GetAsync(Relative(path), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<T>(response);
    }

    private static async Task AssertStatusAsync(HttpClient client, HttpMethod method, string path, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(method, Relative(path));
        using HttpResponseMessage response = await client.SendAsync(request, Ct);
        Assert.Equal(expected, response.StatusCode);
    }

    private static async Task<CategoryResponse> CreateCategoryAsync(HttpClient client, string name)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(Relative("api/v1/categories"), new CategoryUpsertRequest(name, null, null), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<CategoryResponse>(response);
    }

    private static async Task<ProductResponse> CreateProductAsync(HttpClient client, string name, Guid categoryId)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(Relative("api/v1/products"), new ProductUpsertRequest(name, null, null, categoryId, 10m, 1), Ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<ProductResponse>(response);
    }
}
