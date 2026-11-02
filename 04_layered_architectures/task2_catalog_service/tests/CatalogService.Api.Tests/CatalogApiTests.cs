using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CatalogService.Api.Tests;

public sealed class CatalogApiTests : IClassFixture<CatalogApiFactory>
{
    private readonly HttpClient _client;

    public CatalogApiTests(CatalogApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostCategory_ReturnsCreatedWithLocation()
    {
        using var response = await _client.PostAsJsonAsync("/api/v1/categories", new { name = $"Category-{Guid.NewGuid():N}" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        using var getResponse = await _client.GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task PostCategory_WithEmptyName_Returns400ProblemDetails()
    {
        using var response = await _client.PostAsJsonAsync("/api/v1/categories", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task PutCategory_WhenMissing_Returns404()
    {
        using var response = await _client.PutAsJsonAsync($"/api/v1/categories/{Guid.NewGuid()}", new { name = "Missing" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_RemovesItsProducts()
    {
        var categoryId = await CreateCategoryAsync();
        var productId = await CreateProductAsync(categoryId);

        using var deleteResponse = await _client.DeleteAsync($"/api/v1/categories/{categoryId}");
        using var productResponse = await _client.GetAsync($"/api/v1/products/{productId}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, productResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithSubcategories_Returns409AndKeepsProducts()
    {
        var parentId = await CreateCategoryAsync();
        using var childResponse = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = $"Child-{Guid.NewGuid():N}",
            parentCategoryId = parentId
        });
        Assert.Equal(HttpStatusCode.Created, childResponse.StatusCode);
        var productId = await CreateProductAsync(parentId);

        using var deleteResponse = await _client.DeleteAsync($"/api/v1/categories/{parentId}");
        using var productResponse = await _client.GetAsync($"/api/v1/products/{productId}");

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, productResponse.StatusCode);
    }

    [Fact]
    public async Task GetProducts_FiltersAndPagesWithHypermediaLinks()
    {
        var categoryId = await CreateCategoryAsync();
        await CreateProductAsync(categoryId);
        await CreateProductAsync(categoryId);
        var otherCategoryId = await CreateCategoryAsync();
        await CreateProductAsync(otherCategoryId);

        using var response = await _client.GetAsync($"/api/v1/products?categoryId={categoryId}&pageNumber=1&pageSize=1");
        var page = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32());
        Assert.Single(page.GetProperty("items").EnumerateArray());
        var nextLink = Assert.Single(page.GetProperty("links").EnumerateArray(), link => link.GetProperty("rel").GetString() == "next");
        var nextUri = new Uri(nextLink.GetProperty("href").GetString()!);
        using var nextResponse = await _client.GetAsync(nextUri.PathAndQuery);
        Assert.Equal(HttpStatusCode.OK, nextResponse.StatusCode);
    }

    [Fact]
    public async Task GetProducts_WithInvalidPageSize_Returns400()
    {
        using var response = await _client.GetAsync("/api/v1/products?pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetProduct_ContainsHypermediaLinks()
    {
        var categoryId = await CreateCategoryAsync();
        var productId = await CreateProductAsync(categoryId);

        using var response = await _client.GetAsync($"/api/v1/products/{productId}");
        var product = await ReadJsonAsync(response);
        var relations = product.GetProperty("links").EnumerateArray()
            .Select(link => link.GetProperty("rel").GetString())
            .ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("self", relations);
        Assert.Contains("update", relations);
        Assert.Contains("delete", relations);
        Assert.Contains("category", relations);
    }

    private async Task<Guid> CreateCategoryAsync()
    {
        using var response = await _client.PostAsJsonAsync("/api/v1/categories", new { name = $"Category-{Guid.NewGuid():N}" });
        response.EnsureSuccessStatusCode();
        var category = await ReadJsonAsync(response);
        return category.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateProductAsync(Guid categoryId)
    {
        using var response = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name = $"Product-{Guid.NewGuid():N}",
            categoryId,
            price = 9.99m,
            amount = 1
        });
        response.EnsureSuccessStatusCode();
        var product = await ReadJsonAsync(response);
        return product.GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }
}
