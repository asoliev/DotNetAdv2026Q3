using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.Tests;

public sealed class GatewayRoutingTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CategoriesGetIsForwarded()
    {
        await using DownstreamStub downstream = await DownstreamStub.StartAsync(Ct);
        const string expectedJson = "{\"categories\":[{\"id\":\"fixture-category\"}]}";
        downstream.SetResponse(HttpMethod.Get, "/api/v1/categories", HttpStatusCode.Accepted, expectedJson);
        await using var factory = new GatewayApiFactory(downstream);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri("/api/v1/categories?source=fixture", UriKind.Relative), Ct);
        string actualJson = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expectedJson, actualJson);

        DownstreamRequest forwarded = Assert.Single(downstream.Requests);
        Assert.Equal(HttpMethod.Get, forwarded.Method);
        Assert.Equal("/api/v1/categories", forwarded.Path);
        Assert.Equal("?source=fixture", forwarded.Query);
        Assert.Equal(1, downstream.GetRequestCount(HttpMethod.Get, "/api/v1/categories"));
    }

    [Fact]
    public async Task InvalidDestinationSettingsFailStartupClearly()
    {
        await using DownstreamStub downstream = await DownstreamStub.StartAsync(Ct);
        await using var factory = new GatewayApiFactory(
            downstream,
            configurationOverrides: new Dictionary<string, string?>
            {
                ["Downstream:Catalog:BaseUrl"] = "ftp://invalid.example",
            });

        Exception? startupError = Record.Exception(() => factory.CreateClient());

        Assert.NotNull(startupError);
        Assert.Contains("Downstream:Catalog:BaseUrl", startupError.ToString(), StringComparison.Ordinal);
        Assert.Contains("absolute HTTP(S) URL", startupError.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CatalogReadRoutesForwardQueriesAndKeepProductRoutesDistinct()
    {
        await using DownstreamStub downstream = await DownstreamStub.StartAsync(Ct);
        await using var factory = new GatewayApiFactory(downstream);
        using HttpClient client = factory.CreateClient();
        Guid categoryId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();

        await ForwardAndAssertAsync(client, downstream, HttpMethod.Get, "/api/v1/categories?pageNumber=2", "/api/v1/categories", "?pageNumber=2", HttpStatusCode.OK, "[]");
        await ForwardAndAssertAsync(client, downstream, HttpMethod.Get, $"/api/v1/categories/{categoryId}", $"/api/v1/categories/{categoryId}", "", HttpStatusCode.OK, "{\"id\":\"category\"}");
        await ForwardAndAssertAsync(client, downstream, HttpMethod.Get, $"/api/v1/categories/{categoryId}/products?pageNumber=2&pageSize=3", $"/api/v1/categories/{categoryId}/products", "?pageNumber=2&pageSize=3", HttpStatusCode.OK, "{\"items\":[]}");
        await ForwardAndAssertAsync(client, downstream, HttpMethod.Get, "/api/v1/products?categoryId=abc&pageNumber=3&pageSize=4", "/api/v1/products", "?categoryId=abc&pageNumber=3&pageSize=4", HttpStatusCode.OK, "{\"items\":[]}");
        await ForwardAndAssertAsync(client, downstream, HttpMethod.Get, $"/api/v1/products/{productId}", $"/api/v1/products/{productId}", "", HttpStatusCode.OK, "{\"id\":\"product\"}");
        await ForwardAndAssertAsync(client, downstream, HttpMethod.Get, $"/api/v1/products/{productId}/properties", $"/api/v1/products/{productId}/properties", "", HttpStatusCode.OK, "{\"model\":\"s10\"}");

        downstream.ResetRequests();
        using HttpResponseMessage aggregate = await client.GetAsync(new Uri($"/api/v1/products/{productId}/aggregate", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, aggregate.StatusCode);
        using JsonDocument aggregateDocument = JsonDocument.Parse(await aggregate.Content.ReadAsStringAsync(Ct));
        Assert.Equal("product", aggregateDocument.RootElement.GetProperty("product").GetProperty("id").GetString());
        Assert.Equal("s10", aggregateDocument.RootElement.GetProperty("properties").GetProperty("model").GetString());
        Assert.Equal(2, downstream.Requests.Count);
        Assert.Contains(downstream.Requests, request => request.Path == $"/api/v1/products/{productId}");
        Assert.Contains(downstream.Requests, request => request.Path == $"/api/v1/products/{productId}/properties");
    }

    [Fact]
    public async Task CatalogWriteRoutesRequireAdminAndForwardMethodsBodiesAndIds()
    {
        await using DownstreamStub downstream = await DownstreamStub.StartAsync(Ct);
        await using var factory = new GatewayApiFactory(downstream);
        using HttpClient client = factory.CreateClient();
        Guid categoryId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();

        using var anonymousCreate = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/categories", UriKind.Relative))
        {
            Content = new StringContent("{\"name\":\"blocked\"}", Encoding.UTF8, "application/json"),
        };
        using HttpResponseMessage denied = await client.SendAsync(anonymousCreate, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(0, downstream.TotalRequestCount);

        string adminToken = TestTokens.Admin;
        (HttpMethod Method, string Path, string DownstreamPath, string Body, HttpStatusCode StatusCode, string ResponseBody)[] writes =
        [
            (HttpMethod.Post, "/api/v1/categories", "/api/v1/categories", "{\"name\":\"category\"}", HttpStatusCode.Created, "{\"id\":\"category-created\"}"),
            (HttpMethod.Put, $"/api/v1/categories/{categoryId}", $"/api/v1/categories/{categoryId}", "{\"name\":\"updated\"}", HttpStatusCode.Accepted, "{\"updated\":true}"),
            (HttpMethod.Patch, $"/api/v1/categories/{categoryId}", $"/api/v1/categories/{categoryId}", "{\"name\":\"patched\"}", HttpStatusCode.Accepted, "{\"patched\":true}"),
            (HttpMethod.Delete, $"/api/v1/categories/{categoryId}", $"/api/v1/categories/{categoryId}", "", HttpStatusCode.Accepted, "{\"deleted\":true}"),
            (HttpMethod.Post, "/api/v1/products", "/api/v1/products", "{\"name\":\"product\"}", HttpStatusCode.Created, "{\"id\":\"product-created\"}"),
            (HttpMethod.Put, $"/api/v1/products/{productId}", $"/api/v1/products/{productId}", "{\"name\":\"updated\"}", HttpStatusCode.Accepted, "{\"updated\":true}"),
            (HttpMethod.Patch, $"/api/v1/products/{productId}", $"/api/v1/products/{productId}", "{\"name\":\"patched\"}", HttpStatusCode.Accepted, "{\"patched\":true}"),
            (HttpMethod.Delete, $"/api/v1/products/{productId}", $"/api/v1/products/{productId}", "", HttpStatusCode.Accepted, "{\"deleted\":true}"),
        ];

        foreach ((HttpMethod method, string path, string downstreamPath, string body, HttpStatusCode statusCode, string responseBody) in writes)
        {
            await ForwardAndAssertAsync(
                client,
                downstream,
                method,
                path,
                downstreamPath,
                string.Empty,
                statusCode,
                responseBody,
                body,
                adminToken);
        }
    }

    [Fact]
    public async Task CartV1AndV2RoutesForwardMethodsBodiesIdsAndAuthorization()
    {
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(Ct);
        await using DownstreamStub cart = await DownstreamStub.StartAsync(Ct);
        await using var factory = new GatewayApiFactory(catalog, cart);
        using HttpClient client = factory.CreateClient();
        string managerToken = TestTokens.Manager;
        Guid itemId = Guid.NewGuid();
        (string Path, string DownstreamPath, HttpMethod Method, string Query, string Body)[] requests =
        [
            ("/api/v1/carts/cart-blue?view=full", "/api/v1/carts/cart-blue", HttpMethod.Get, "?view=full", ""),
            ("/api/v1/carts/cart-blue/items", "/api/v1/carts/cart-blue/items", HttpMethod.Post, "", "{\"id\":\"item-v1\"}"),
            ($"/api/v1/carts/cart-blue/items/{itemId}", $"/api/v1/carts/cart-blue/items/{itemId}", HttpMethod.Delete, "", ""),
            ("/api/v2/carts/cart-green?view=compact", "/api/v2/carts/cart-green", HttpMethod.Get, "?view=compact", ""),
            ("/api/v2/carts/cart-green/items", "/api/v2/carts/cart-green/items", HttpMethod.Post, "", "{\"id\":\"item-v2\"}"),
            ($"/api/v2/carts/cart-green/items/{itemId}", $"/api/v2/carts/cart-green/items/{itemId}", HttpMethod.Delete, "", ""),
        ];

        foreach ((string path, string downstreamPath, HttpMethod method, string query, string body) in requests)
        {
            await ForwardAndAssertAsync(
                client,
                cart,
                method,
                path,
                downstreamPath,
                query,
                HttpStatusCode.Accepted,
                "{\"forwarded\":true}",
                body,
                managerToken);
        }
    }

    [Fact]
    public async Task UnsupportedCartVersionsDoNotReachDownstream()
    {
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(Ct);
        await using DownstreamStub cart = await DownstreamStub.StartAsync(Ct);
        await using var factory = new GatewayApiFactory(catalog, cart);
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokens.Manager);
        Guid itemId = Guid.NewGuid();
        (HttpMethod Method, string Path, string? Body)[] unsupported =
        [
            (HttpMethod.Get, "/api/v3/carts/cart-unknown", null),
            (HttpMethod.Post, "/api/v3/carts/cart-unknown/items", "{}"),
            (HttpMethod.Delete, $"/api/v3/carts/cart-unknown/items/{itemId}", null),
        ];

        foreach ((HttpMethod method, string path, string? body) in unsupported)
        {
            using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
            if (body is not null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            using HttpResponseMessage response = await client.SendAsync(request, Ct);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        Assert.Empty(cart.Requests);
    }

    [Fact]
    public async Task CatalogCreateLocationUsesGatewayAndPreservesPathAndQuery()
    {
        await using DownstreamStub unusedStub = await DownstreamStub.StartAsync(Ct);
        Guid categoryId = Guid.NewGuid();
        await using LocationResponseServer downstream = await LocationResponseServer.StartAsync(categoryId, Ct);
        await using var factory = new GatewayApiFactory(
            unusedStub,
            configurationOverrides: new Dictionary<string, string?>
            {
                ["Downstream:Catalog:BaseUrl"] = downstream.BaseAddress.ToString(),
            });
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("/api/v1/categories", UriKind.Relative))
        {
            Content = new StringContent("{\"name\":\"created\"}", Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", TestTokens.Admin);

        using HttpResponseMessage created = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Uri location = Assert.IsType<Uri>(created.Headers.Location);
        Assert.Equal("http://localhost:5004", location.GetLeftPart(UriPartial.Authority));
        Assert.Equal($"/api/v1/categories/{categoryId}?source=created", location.PathAndQuery);

        using HttpResponseMessage loaded = await client.GetAsync(new Uri(location.PathAndQuery, UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.OK, loaded.StatusCode);
        Assert.Equal("{\"id\":\"category\"}", await loaded.Content.ReadAsStringAsync(Ct));
    }

    private static async Task<DownstreamRequest> ForwardAndAssertAsync(
        HttpClient client,
        DownstreamStub downstream,
        HttpMethod method,
        string requestPath,
        string downstreamPath,
        string expectedQuery,
        HttpStatusCode responseStatus,
        string responseBody,
        string? requestBody = null,
        string? accessToken = null)
    {
        downstream.ResetRequests();
        downstream.SetResponse(method, downstreamPath, responseStatus, responseBody);
        using var request = new HttpRequestMessage(method, new Uri(requestPath, UriKind.Relative));
        if (requestBody is not null)
        {
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
        }

        if (accessToken is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        using HttpResponseMessage response = await client.SendAsync(request, Ct);
        Assert.Equal(responseStatus, response.StatusCode);
        Assert.Equal(responseBody, await response.Content.ReadAsStringAsync(Ct));

        DownstreamRequest forwarded = Assert.Single(downstream.Requests);
        Assert.Equal(method, forwarded.Method);
        Assert.Equal(downstreamPath, forwarded.Path);
        Assert.Equal(expectedQuery, forwarded.Query);
        Assert.Equal(requestBody ?? string.Empty, forwarded.Body);
        if (accessToken is null)
        {
            Assert.Null(forwarded.AuthorizationSha256);
        }
        else
        {
            Assert.Equal(AuthorizationFingerprint(accessToken), forwarded.AuthorizationSha256);
            Assert.Equal("<redacted>", Assert.Single(forwarded.Headers["Authorization"]));
        }

        return forwarded;
    }

    private static string AuthorizationFingerprint(string accessToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"Bearer {accessToken}")));

    private sealed class LocationResponseServer(WebApplication application, Uri baseAddress) : IAsyncDisposable
    {
        public Uri BaseAddress { get; } = baseAddress;

        public static async Task<LocationResponseServer> StartAsync(Guid categoryId, CancellationToken cancellationToken)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options => options.Listen(System.Net.IPAddress.Loopback, 0));
            WebApplication application = builder.Build();
            string downstreamBaseUrl = string.Empty;
            application.Run(async context =>
            {
                context.Response.ContentType = "application/json";
                if (HttpMethods.IsPost(context.Request.Method))
                {
                    context.Response.StatusCode = StatusCodes.Status201Created;
                    context.Response.Headers.Location = $"{downstreamBaseUrl}/api/v1/categories/{categoryId}?source=created";
                    await context.Response.WriteAsync("{\"id\":\"category\"}", context.RequestAborted);
                    return;
                }

                context.Response.StatusCode = StatusCodes.Status200OK;
                await context.Response.WriteAsync("{\"id\":\"category\"}", context.RequestAborted);
            });

            await application.StartAsync(cancellationToken);
            IServer server = application.Services.GetRequiredService<IServer>();
            string address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
                ?? throw new InvalidOperationException("The Location test server did not expose its loopback address.");
            downstreamBaseUrl = address.TrimEnd('/');
            return new LocationResponseServer(application, new Uri(address));
        }

        public async ValueTask DisposeAsync() => await application.DisposeAsync();
    }
}