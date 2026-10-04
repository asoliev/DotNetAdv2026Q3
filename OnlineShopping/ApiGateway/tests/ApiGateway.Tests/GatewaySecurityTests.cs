using System.Net;
using System.Security.Cryptography;
using System.Text;

using System.Net.Http.Headers;

using ShoppingAuth;

namespace ApiGateway.Tests;

public sealed class GatewaySecurityTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CatalogMutationMatrixRejectsInvalidAndInsufficientCallersWithoutForwarding()
    {
        await using DownstreamStub downstream = await DownstreamStub.StartAsync(Ct);
        await using var factory = new GatewayApiFactory(downstream);
        using HttpClient client = factory.CreateClient();
        GatewayRequest[] mutations = CatalogMutations();
        SecurityCaller[] rejectedCallers =
        [
            new("anonymous", null, HttpStatusCode.Unauthorized),
            new("forged", TestTokens.Create(AuthRoles.Admin, signingKey: "invalid-signing-key-for-gateway-tests"), HttpStatusCode.Unauthorized),
            new("expired", TestTokens.Create(AuthRoles.Admin, expires: DateTime.UtcNow.AddMinutes(-5)), HttpStatusCode.Unauthorized),
            new("wrong issuer", TestTokens.Create(AuthRoles.Admin, issuer: "untrusted-issuer"), HttpStatusCode.Unauthorized),
            new("wrong audience", TestTokens.Create(AuthRoles.Admin, audience: "untrusted-audience"), HttpStatusCode.Unauthorized),
            new("Store customer", TestTokens.StoreCustomer, HttpStatusCode.Forbidden),
            new("Manager", TestTokens.Manager, HttpStatusCode.Forbidden),
            new("anonymous with role headers", null, HttpStatusCode.Unauthorized, SpoofRoleHeaders: true),
            new("Manager with role headers", TestTokens.Manager, HttpStatusCode.Forbidden, SpoofRoleHeaders: true),
        ];

        foreach (GatewayRequest mutation in mutations)
        {
            foreach (SecurityCaller caller in rejectedCallers)
            {
                downstream.ResetRequests();
                using HttpRequestMessage request = CreateRequest(mutation, caller.AccessToken, caller.SpoofRoleHeaders);
                using HttpResponseMessage response = await client.SendAsync(request, Ct);

                Assert.True(
                    response.StatusCode == caller.ExpectedStatus,
                    $"{caller.Name} received {(int)response.StatusCode} for {mutation.Method} {mutation.Path}; expected {(int)caller.ExpectedStatus}.");
                Assert.Equal(0, downstream.TotalRequestCount);
            }
        }
    }

    [Fact]
    public async Task AdminReachesEveryCatalogMutationWithAuthorizationIntact()
    {
        await using DownstreamStub downstream = await DownstreamStub.StartAsync(Ct);
        await using var factory = new GatewayApiFactory(downstream);
        using HttpClient client = factory.CreateClient();
        string adminToken = TestTokens.Admin;

        foreach (GatewayRequest mutation in CatalogMutations())
        {
            downstream.ResetRequests();
            downstream.SetResponse(mutation.Method, mutation.Path, HttpStatusCode.Accepted, "{\"accepted\":true}");
            using HttpRequestMessage request = CreateRequest(mutation, adminToken);
            using HttpResponseMessage response = await client.SendAsync(request, Ct);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            Assert.Equal("{\"accepted\":true}", await response.Content.ReadAsStringAsync(Ct));
            DownstreamRequest forwarded = Assert.Single(downstream.Requests);
            Assert.Equal(mutation.Method, forwarded.Method);
            Assert.Equal(mutation.Path, forwarded.Path);
            Assert.Equal(mutation.Body ?? string.Empty, forwarded.Body);
            Assert.Equal(AuthorizationFingerprint(adminToken), forwarded.AuthorizationSha256);
            Assert.Equal("<redacted>", Assert.Single(forwarded.Headers["Authorization"]));
        }
    }

    [Fact]
    public async Task CatalogReadRoutesRemainAnonymous()
    {
        await using DownstreamStub downstream = await DownstreamStub.StartAsync(Ct);
        await using var factory = new GatewayApiFactory(downstream);
        using HttpClient client = factory.CreateClient();
        Guid categoryId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();
        GatewayRequest[] reads =
        [
            new(HttpMethod.Get, "/api/v1/categories", null),
            new(HttpMethod.Get, $"/api/v1/categories/{categoryId}", null),
            new(HttpMethod.Get, $"/api/v1/categories/{categoryId}/products?pageNumber=1&pageSize=20", null),
            new(HttpMethod.Get, "/api/v1/products?pageNumber=1&pageSize=20", null),
            new(HttpMethod.Get, $"/api/v1/products/{productId}", null),
            new(HttpMethod.Get, $"/api/v1/products/{productId}/properties", null),
        ];

        foreach (GatewayRequest read in reads)
        {
            downstream.ResetRequests();
            downstream.SetResponse(HttpMethod.Get, PathOnly(read.Path), HttpStatusCode.OK, "{\"read\":true}");
            using HttpRequestMessage request = CreateRequest(read, accessToken: null);
            using HttpResponseMessage response = await client.SendAsync(request, Ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            DownstreamRequest forwarded = Assert.Single(downstream.Requests);
            Assert.Equal(read.Path.Split('?')[0], forwarded.Path);
            Assert.Null(forwarded.AuthorizationSha256);
        }
    }

    [Fact]
    public async Task CartRoutesRequireGatewayJwtAndForwardValidJwtWithoutClaimingServiceAuthorization()
    {
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(Ct);
        await using DownstreamStub cart = await DownstreamStub.StartAsync(Ct);
        await using var factory = new GatewayApiFactory(catalog, cart);
        using HttpClient client = factory.CreateClient();
        GatewayRequest[] cartRoutes = CartRoutes();
        SecurityCaller[] invalidCallers =
        [
            new("anonymous", null, HttpStatusCode.Unauthorized),
            new("forged", TestTokens.Create(AuthRoles.Manager, signingKey: "invalid-signing-key-for-gateway-tests"), HttpStatusCode.Unauthorized),
        ];

        foreach (GatewayRequest route in cartRoutes)
        {
            foreach (SecurityCaller caller in invalidCallers)
            {
                cart.ResetRequests();
                using HttpRequestMessage request = CreateRequest(route, caller.AccessToken);
                using HttpResponseMessage response = await client.SendAsync(request, Ct);

                Assert.Equal(caller.ExpectedStatus, response.StatusCode);
                Assert.Equal(0, cart.TotalRequestCount);
            }
        }

        string managerToken = TestTokens.Manager;
        foreach (GatewayRequest route in cartRoutes)
        {
            cart.ResetRequests();
            cart.SetResponse(route.Method, PathOnly(route.Path), HttpStatusCode.Accepted, "{\"accepted\":true}");
            using HttpRequestMessage request = CreateRequest(route, managerToken);
            using HttpResponseMessage response = await client.SendAsync(request, Ct);

            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            DownstreamRequest forwarded = Assert.Single(cart.Requests);
            Assert.Equal(route.Method, forwarded.Method);
            Assert.Equal(PathOnly(route.Path), forwarded.Path);
            Assert.Equal(route.Body ?? string.Empty, forwarded.Body);
            Assert.Equal(AuthorizationFingerprint(managerToken), forwarded.AuthorizationSha256);
        }
    }

    private static GatewayRequest[] CatalogMutations()
    {
        Guid categoryId = Guid.NewGuid();
        Guid productId = Guid.NewGuid();
        string categoryBody = "{\"name\":\"Books\",\"image\":null,\"parentCategoryId\":null}";
        string productBody = $"{{\"name\":\"Phone\",\"description\":null,\"image\":null,\"categoryId\":\"{categoryId}\",\"price\":10.0,\"amount\":1}}";

        return
        [
            new(HttpMethod.Post, "/api/v1/categories", categoryBody),
            new(HttpMethod.Put, $"/api/v1/categories/{categoryId}", categoryBody),
            new(HttpMethod.Patch, $"/api/v1/categories/{categoryId}", "{\"name\":\"patched\"}"),
            new(HttpMethod.Delete, $"/api/v1/categories/{categoryId}", null),
            new(HttpMethod.Post, "/api/v1/products", productBody),
            new(HttpMethod.Put, $"/api/v1/products/{productId}", productBody),
            new(HttpMethod.Patch, $"/api/v1/products/{productId}", "{\"name\":\"patched\"}"),
            new(HttpMethod.Delete, $"/api/v1/products/{productId}", null),
        ];
    }

    private static GatewayRequest[] CartRoutes()
    {
        Guid itemId = Guid.NewGuid();
        return
        [
            new(HttpMethod.Get, "/api/v1/carts/cart-v1", null),
            new(HttpMethod.Post, "/api/v1/carts/cart-v1/items", "{\"id\":\"item-v1\"}"),
            new(HttpMethod.Delete, $"/api/v1/carts/cart-v1/items/{itemId}", null),
            new(HttpMethod.Get, "/api/v2/carts/cart-v2", null),
            new(HttpMethod.Post, "/api/v2/carts/cart-v2/items", "{\"id\":\"item-v2\"}"),
            new(HttpMethod.Delete, $"/api/v2/carts/cart-v2/items/{itemId}", null),
        ];
    }

    private static HttpRequestMessage CreateRequest(GatewayRequest route, string? accessToken, bool spoofRoleHeaders = false)
    {
        var request = new HttpRequestMessage(route.Method, new Uri(route.Path, UriKind.Relative));
        if (route.Body is not null)
        {
            request.Content = new StringContent(route.Body, Encoding.UTF8, "application/json");
        }

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (spoofRoleHeaders)
        {
            request.Headers.TryAddWithoutValidation("X-Role", AuthRoles.Admin);
            request.Headers.TryAddWithoutValidation("role", AuthRoles.Admin);
        }

        return request;
    }

    private static string PathOnly(string path) => path.Split('?')[0];

    private static string AuthorizationFingerprint(string accessToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"Bearer {accessToken}")));

    private sealed record GatewayRequest(HttpMethod Method, string Path, string? Body);

    private sealed record SecurityCaller(string Name, string? AccessToken, HttpStatusCode ExpectedStatus, bool SpoofRoleHeaders = false);
}