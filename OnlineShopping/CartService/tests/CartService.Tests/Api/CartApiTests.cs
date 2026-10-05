using System.Net;
using System.Net.Http.Json;

using CartService.Api;
using CartService.Api.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace CartService.Tests.Api;

public sealed class CartApiTests(CartApiFactory factory) : IClassFixture<CartApiFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task SwaggerDocumentsAreServed(string version)
    {
        using HttpClient client = factory.CreateClient(authorization: null);

        var payload = await client.GetStringAsync(Relative($"/swagger/{version}/swagger.json"), Ct);

        Assert.Contains("Cart Service API", payload, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer not-a-jwt")]
    [InlineData("Basic dXNlcjpwYXNz")]
    public async Task CartRequiresAValidAccessToken(string? authorization)
    {
        using HttpClient client = factory.CreateClient(authorization);

        using HttpResponseMessage response = await client.GetAsync(Relative("api/v1/carts/anonymous"), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Matches("^[0-9a-f]{32}$", Assert.Single(response.Headers.GetValues("X-Trace-Id")));
    }

    [Fact]
    public async Task ManagerAndStoreCustomerCanReadCartsAndVersionsAreReported()
    {
        using HttpClient manager = factory.CreateClient($"Bearer {TestTokens.Manager}");
        using HttpClient customer = factory.CreateClient($"Bearer {TestTokens.StoreCustomer}");

        using HttpResponseMessage managerResponse = await manager.GetAsync(Relative("api/v1/carts/empty-cart"), Ct);
        using HttpResponseMessage customerResponse = await customer.GetAsync(Relative("api/v2/carts/empty-cart"), Ct);
        using HttpResponseMessage unsupported = await customer.GetAsync(Relative("api/v3/carts/empty-cart"), Ct);

        Assert.Equal(HttpStatusCode.OK, managerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, customerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unsupported.StatusCode);
        Assert.Equal("1.0, 2.0", Assert.Single(managerResponse.Headers.GetValues("api-supported-versions")));
        CartResponse cart = await ReadAsync<CartResponse>(managerResponse);
        Assert.Equal("empty-cart", cart.CartKey);
        Assert.Empty(cart.Items);
        Assert.Empty(await ReadAsync<List<CartItemResponse>>(customerResponse));
    }

    [Fact]
    public async Task ItemsCanBeAddedMergedReadAndRemovedAcrossVersions()
    {
        using HttpClient client = factory.CreateClient($"Bearer {TestTokens.StoreCustomer}");
        var cartKey = $"cart-{Guid.NewGuid():N}";
        var itemId = Guid.NewGuid();
        var withImage = new CartItemRequest(itemId, "Phone", new CartItemImageRequest(new Uri("https://example.com/phone.png"), "Phone"), 499.99m, 2);

        using HttpResponseMessage added = await client.PostAsJsonAsync(Relative($"api/v1/carts/{cartKey}/items"), withImage, Ct);
        using HttpResponseMessage merged = await client.PostAsJsonAsync(Relative($"api/v2/carts/{cartKey}/items"), withImage with { Quantity = 3 }, Ct);

        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        Assert.Equal(new Uri("https://example.com/phone.png"), Assert.Single((await ReadAsync<CartResponse>(added)).Items).Image?.Url);
        Assert.Equal(5, Assert.Single((await ReadAsync<CartResponse>(merged)).Items).Quantity);

        CartResponse v1 = await GetAsync<CartResponse>(client, $"api/v1/carts/{cartKey}");
        List<CartItemResponse> v2 = await GetAsync<List<CartItemResponse>>(client, $"api/v2/carts/{cartKey}");
        Assert.Equal(cartKey, v1.CartKey);
        Assert.Equal(itemId, Assert.Single(v1.Items).Id);
        Assert.Equal(v1.Items, v2);

        using HttpResponseMessage removed = await client.DeleteAsync(Relative($"api/v1/carts/{cartKey}/items/{itemId}"), Ct);
        using HttpResponseMessage removedAgain = await client.DeleteAsync(Relative($"api/v2/carts/{cartKey}/items/{itemId}"), Ct);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removedAgain.StatusCode);
        Assert.Empty((await GetAsync<CartResponse>(client, $"api/v1/carts/{cartKey}")).Items);
    }

    [Fact]
    public async Task ItemWithoutImageIsStored()
    {
        using HttpClient client = factory.CreateClient($"Bearer {TestTokens.Manager}");
        var cartKey = $"cart-{Guid.NewGuid():N}";

        using HttpResponseMessage added = await client.PostAsJsonAsync(Relative($"api/v1/carts/{cartKey}/items"), new CartItemRequest(Guid.NewGuid(), "Cable", null, 5m, 1), Ct);

        Assert.Equal(HttpStatusCode.OK, added.StatusCode);
        Assert.Null(Assert.Single((await ReadAsync<CartResponse>(added)).Items).Image);
    }

    [Fact]
    public async Task MissingBodyIsRejected()
    {
        using HttpClient client = factory.CreateClient($"Bearer {TestTokens.Manager}");
        using var empty = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(Relative("api/v1/carts/cart-1/items"), empty, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("""{ "name": "Mouse", "price": 25.0, "quantity": 1 }""")]
    [InlineData("""{ "id": "7f8e0a52-8d2c-4a39-9b0e-2f8f3c1d4a10", "name": "Mouse", "quantity": 1 }""")]
    [InlineData("""{ "id": "7f8e0a52-8d2c-4a39-9b0e-2f8f3c1d4a10", "name": "Mouse", "price": 25.0 }""")]
    public async Task ItemWithMissingRequiredFieldIsRejected(string json)
    {
        using HttpClient client = factory.CreateClient($"Bearer {TestTokens.Manager}");
        using var body = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        using HttpResponseMessage response = await client.PostAsync(Relative("api/v1/carts/cart-1/items"), body, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task MiddlewareRejectsMissingDependencies()
    {
        Assert.Throws<ArgumentNullException>(() => new AccessTokenLoggingMiddleware(null!, NullLogger<AccessTokenLoggingMiddleware>.Instance));
        Assert.Throws<ArgumentNullException>(() => new AccessTokenLoggingMiddleware(_ => Task.CompletedTask, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => new AccessTokenLoggingMiddleware(_ => Task.CompletedTask, NullLogger<AccessTokenLoggingMiddleware>.Instance).InvokeAsync(null!));
    }

    [Fact]
    public async Task MiddlewarePassesRequestsWithUnreadableTokensThrough()
    {
        var called = false;
        var middleware = new AccessTokenLoggingMiddleware(_ => { called = true; return Task.CompletedTask; }, NullLogger<AccessTokenLoggingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer a.b";

        await middleware.InvokeAsync(context);

        Assert.True(called);
    }

    private static Uri Relative(string path) => new(path, UriKind.Relative);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) => await response.Content.ReadFromJsonAsync<T>(Ct) ?? throw new InvalidOperationException("Empty response body.");

    private static async Task<T> GetAsync<T>(HttpClient client, string path)
    {
        using HttpResponseMessage response = await client.GetAsync(Relative(path), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<T>(response);
    }
}
