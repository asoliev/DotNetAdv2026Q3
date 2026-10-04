using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

using ShoppingAuth;

namespace ApiGateway.Tests;

public sealed class GatewayCacheTests
{
    private static readonly string[] ExpectedCachedRouteKeys =
    [
        "catalog-categories-list",
        "catalog-category-products-list",
        "catalog-products-list",
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ProductionRouteJsonCachesOnlyTheThreeCatalogListGetsForSixtySeconds()
    {
        using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(RouteJsonPath(), Ct));
        JsonElement[] cachedRoutes = document.RootElement
            .GetProperty("Routes")
            .EnumerateArray()
            .Where(route => route.TryGetProperty("CacheOptions", out _))
            .ToArray();

        string[] actualKeys = cachedRoutes
            .Select(route => route.GetProperty("Key").GetString()!)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();
        string[] expectedKeys = ExpectedCachedRouteKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray();

        Assert.Equal(expectedKeys, actualKeys);
        foreach (JsonElement route in cachedRoutes)
        {
            Assert.Equal(60, route.GetProperty("CacheOptions").GetProperty("TtlSeconds").GetInt32());
            Assert.Equal(["GET"], route.GetProperty("UpstreamHttpMethod").EnumerateArray().Select(method => method.GetString()).ToArray());
        }
    }

    [Fact]
    public async Task ListCacheHitsAndSeparatesQueryAndRouteKeys()
    {
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(Ct);
        await using var factory = new GatewayApiFactory(catalog);
        using HttpClient client = factory.CreateClient();
        Guid firstCategoryId = Guid.NewGuid();
        Guid secondCategoryId = Guid.NewGuid();

        catalog.SetResponse(HttpMethod.Get, "/api/v1/categories", HttpStatusCode.OK, "{\"list\":\"categories\"}");
        string categoriesPageOne = "/api/v1/categories?pageNumber=1&pageSize=10";
        await AssertGetAsync(client, categoriesPageOne);
        await AssertGetAsync(client, categoriesPageOne);
        Assert.Equal(1, catalog.GetRequestCount(HttpMethod.Get, "/api/v1/categories"));
        await AssertGetAsync(client, "/api/v1/categories?pageNumber=2&pageSize=10");
        Assert.Equal(2, catalog.GetRequestCount(HttpMethod.Get, "/api/v1/categories"));
        await AssertGetAsync(client, "/api/v1/categories?pageNumber=1&pageSize=20");
        Assert.Equal(3, catalog.GetRequestCount(HttpMethod.Get, "/api/v1/categories"));

        catalog.ResetRequests();
        catalog.SetResponse(HttpMethod.Get, "/api/v1/products", HttpStatusCode.OK, "{\"list\":\"products\"}");
        string productQueryOne = $"/api/v1/products?categoryId={firstCategoryId}&pageNumber=1&pageSize=10";
        await AssertGetAsync(client, productQueryOne);
        await AssertGetAsync(client, productQueryOne);
        Assert.Equal(1, catalog.GetRequestCount(HttpMethod.Get, "/api/v1/products"));
        await AssertGetAsync(client, $"/api/v1/products?categoryId={firstCategoryId}&pageNumber=2&pageSize=10");
        await AssertGetAsync(client, $"/api/v1/products?categoryId={firstCategoryId}&pageNumber=1&pageSize=20");
        await AssertGetAsync(client, $"/api/v1/products?categoryId={secondCategoryId}&pageNumber=1&pageSize=10");
        Assert.Equal(4, catalog.GetRequestCount(HttpMethod.Get, "/api/v1/products"));

        catalog.ResetRequests();
        string categoryProductsOne = $"/api/v1/categories/{firstCategoryId}/products?pageNumber=1&pageSize=10";
        catalog.SetResponse(HttpMethod.Get, $"/api/v1/categories/{firstCategoryId}/products", HttpStatusCode.OK, "{\"list\":\"category-products-1\"}");
        catalog.SetResponse(HttpMethod.Get, $"/api/v1/categories/{secondCategoryId}/products", HttpStatusCode.OK, "{\"list\":\"category-products-2\"}");
        await AssertGetAsync(client, categoryProductsOne);
        await AssertGetAsync(client, categoryProductsOne);
        Assert.True(
            catalog.GetRequestCount(HttpMethod.Get, $"/api/v1/categories/{firstCategoryId}/products") == 1,
            string.Join(" | ", catalog.Requests.Select(request => $"{request.Method} {request.Path}{request.Query}")));
        await AssertGetAsync(client, $"/api/v1/categories/{firstCategoryId}/products?pageNumber=2&pageSize=10");
        await AssertGetAsync(client, $"/api/v1/categories/{firstCategoryId}/products?pageNumber=1&pageSize=20");
        string categoryProductsTwo = $"/api/v1/categories/{secondCategoryId}/products?pageNumber=1&pageSize=10";
        await AssertGetAsync(client, categoryProductsTwo);
        await AssertGetAsync(client, categoryProductsTwo);
        await AssertGetAsync(client, categoryProductsOne);
        Assert.Equal(4, catalog.TotalRequestCount);
        Assert.Equal(1, catalog.GetRequestCount(HttpMethod.Get, $"/api/v1/categories/{secondCategoryId}/products"));
    }

    [Fact]
    public async Task CachedListExpiresAndIsFetchedAgain()
    {
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(Ct);
        await using var routeConfiguration = await TemporaryRouteConfiguration.CreateAsync(ttlSeconds: 1, Ct);
        await using var factory = new ShortTtlGatewayFactory(routeConfiguration.ContentRoot, catalog.BaseAddress);
        using HttpClient client = factory.CreateClient();
        const string requestPath = "/api/v1/categories?pageNumber=1&pageSize=5";
        catalog.SetResponse(HttpMethod.Get, "/api/v1/categories", HttpStatusCode.OK, "[]");

        await AssertGetAsync(client, requestPath);
        await AssertGetAsync(client, requestPath);
        Assert.Equal(1, catalog.GetRequestCount(HttpMethod.Get, "/api/v1/categories"));

        await Task.Delay(TimeSpan.FromSeconds(2), Ct);

        await AssertGetAsync(client, requestPath);
        Assert.Equal(2, catalog.GetRequestCount(HttpMethod.Get, "/api/v1/categories"));
    }

    [Fact]
    public async Task ProductDetailsWritesAndCartReadsAreNotCached()
    {
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(Ct);
        await using DownstreamStub cart = await DownstreamStub.StartAsync(Ct);
        await using var factory = new GatewayApiFactory(catalog, cart);
        using HttpClient client = factory.CreateClient();
        Guid productId = Guid.NewGuid();
        Guid categoryId = Guid.NewGuid();

        catalog.SetResponse(HttpMethod.Get, $"/api/v1/categories/{categoryId}", HttpStatusCode.OK, "{\"detail\":true}");
        await AssertGetAsync(client, $"/api/v1/categories/{categoryId}");
        await AssertGetAsync(client, $"/api/v1/categories/{categoryId}");
        Assert.Equal(2, catalog.GetRequestCount(HttpMethod.Get, $"/api/v1/categories/{categoryId}"));

        catalog.SetResponse(HttpMethod.Get, $"/api/v1/products/{productId}", HttpStatusCode.OK, "{\"detail\":true}");
        await AssertGetAsync(client, $"/api/v1/products/{productId}");
        await AssertGetAsync(client, $"/api/v1/products/{productId}");
        Assert.Equal(2, catalog.GetRequestCount(HttpMethod.Get, $"/api/v1/products/{productId}"));

        catalog.SetResponse(HttpMethod.Get, $"/api/v1/products/{productId}/properties", HttpStatusCode.OK, "{\"properties\":true}");
        await AssertGetAsync(client, $"/api/v1/products/{productId}/properties");
        await AssertGetAsync(client, $"/api/v1/products/{productId}/properties");
        Assert.Equal(2, catalog.GetRequestCount(HttpMethod.Get, $"/api/v1/products/{productId}/properties"));

        catalog.SetResponse(HttpMethod.Post, "/api/v1/products", HttpStatusCode.Accepted, "{\"created\":true}");
        using (HttpResponseMessage firstWrite = await SendAsync(client, HttpMethod.Post, "/api/v1/products", "{}", TestTokens.Admin))
        {
            Assert.Equal(HttpStatusCode.Accepted, firstWrite.StatusCode);
        }

        using (HttpResponseMessage secondWrite = await SendAsync(client, HttpMethod.Post, "/api/v1/products", "{}", TestTokens.Admin))
        {
            Assert.Equal(HttpStatusCode.Accepted, secondWrite.StatusCode);
        }

        Assert.Equal(2, catalog.GetRequestCount(HttpMethod.Post, "/api/v1/products"));

        cart.SetResponse(HttpMethod.Get, "/api/v1/carts/cart-cache-test", HttpStatusCode.Accepted, "{\"cart\":true}");
        using (HttpResponseMessage firstCart = await SendAsync(client, HttpMethod.Get, "/api/v1/carts/cart-cache-test", accessToken: TestTokens.Manager))
        {
            Assert.Equal(HttpStatusCode.Accepted, firstCart.StatusCode);
        }

        using (HttpResponseMessage secondCart = await SendAsync(client, HttpMethod.Get, "/api/v1/carts/cart-cache-test", accessToken: TestTokens.Manager))
        {
            Assert.Equal(HttpStatusCode.Accepted, secondCart.StatusCode);
        }

        Assert.Equal(2, cart.GetRequestCount(HttpMethod.Get, "/api/v1/carts/cart-cache-test"));
    }

    private static string RouteJsonPath() => Path.Combine(AppContext.BaseDirectory, "ocelot.json");

    private static async Task AssertGetAsync(HttpClient client, string path)
    {
        using HttpResponseMessage response = await client.GetAsync(new Uri(path, UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, string? body = null, string? accessToken = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
        }

        if (accessToken is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request, Ct);
    }

    private sealed class ShortTtlGatewayFactory(string contentRoot, Uri catalogBaseAddress) : WebApplicationFactory<Program>
    {
        private static readonly object EnvironmentConfigurationLock = new();

        public new HttpClient CreateClient()
        {
            lock (EnvironmentConfigurationLock)
            {
                const string catalogKey = "Downstream__Catalog__BaseUrl";
                const string cartKey = "Downstream__Cart__BaseUrl";
                string? previousCatalogValue = Environment.GetEnvironmentVariable(catalogKey);
                string? previousCartValue = Environment.GetEnvironmentVariable(cartKey);
                try
                {
                    Environment.SetEnvironmentVariable(catalogKey, catalogBaseAddress.ToString());
                    Environment.SetEnvironmentVariable(cartKey, catalogBaseAddress.ToString());
                    return base.CreateClient();
                }
                finally
                {
                    Environment.SetEnvironmentVariable(catalogKey, previousCatalogValue);
                    Environment.SetEnvironmentVariable(cartKey, previousCartValue);
                }
            }
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(contentRoot);
        }
    }

    private sealed class TemporaryRouteConfiguration(DirectoryInfo directory) : IAsyncDisposable
    {
        public string ContentRoot { get; } = directory.FullName;

        public static async Task<TemporaryRouteConfiguration> CreateAsync(int ttlSeconds, CancellationToken cancellationToken)
        {
            string routeJson = await File.ReadAllTextAsync(RouteJsonPath(), cancellationToken);
            JsonObject root = JsonNode.Parse(routeJson)?.AsObject()
                ?? throw new InvalidOperationException("The gateway route JSON could not be parsed.");
            JsonArray routes = root["Routes"]?.AsArray()
                ?? throw new InvalidOperationException("The gateway route JSON has no Routes array.");

            foreach (JsonNode? node in routes)
            {
                if (node is JsonObject route && route["Key"] is JsonValue keyNode && ExpectedCachedRouteKeys.Contains(keyNode.GetValue<string>(), StringComparer.Ordinal))
                {
                    route["CacheOptions"]!["TtlSeconds"] = ttlSeconds;
                }
            }

            DirectoryInfo directory = Directory.CreateTempSubdirectory("gateway-cache-routes-");
            await File.WriteAllTextAsync(
                Path.Combine(directory.FullName, "ocelot.json"),
                root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken);

            string appSettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (File.Exists(appSettings))
            {
                File.Copy(appSettings, Path.Combine(directory.FullName, "appsettings.json"));
            }

            return new TemporaryRouteConfiguration(directory);
        }

        public ValueTask DisposeAsync()
        {
            directory.Delete(recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}