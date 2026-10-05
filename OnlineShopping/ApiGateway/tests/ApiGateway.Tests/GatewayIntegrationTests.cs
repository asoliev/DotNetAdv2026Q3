using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace ApiGateway.Tests;

public sealed class GatewayIntegrationTests
{
    private static readonly string[] ExpectedSwaggerDocumentNames = ["Catalog v1", "Cart v1", "Cart v2"];
    private static readonly string[] ExpectedSwaggerDocumentUrls =
    [
        "/swagger/catalog/v1/swagger.json",
        "/swagger/cart/v1/swagger.json",
        "/swagger/cart/v2/swagger.json",
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", "4bf92f3577b34da6a3ce929d0e0e4736")]
    [InlineData("not-a-traceparent", null)]
    public async Task ProductRoutePropagatesValidTraceContextAndReturnsTraceId(string traceparent, string? expectedTraceId)
    {
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(Ct);
        Guid productId = Guid.NewGuid();
        string path = $"/api/v1/products/{productId}";
        catalog.SetResponse(HttpMethod.Get, path, HttpStatusCode.OK, "{}");
        await using var factory = new GatewayApiFactory(catalog);
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(path, UriKind.Relative));
        request.Headers.TryAddWithoutValidation("traceparent", traceparent);

        using HttpResponseMessage response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("X-Trace-Id", out IEnumerable<string>? responseTraceIds));
        string responseTraceId = Assert.Single(responseTraceIds!);
        Assert.Matches("^[0-9a-f]{32}$", responseTraceId);
        if (expectedTraceId is not null)
        {
            Assert.Equal(expectedTraceId, responseTraceId);
        }

        DownstreamRequest downstreamRequest = Assert.Single(catalog.Requests);
        Assert.True(downstreamRequest.Headers.TryGetValue("traceparent", out string[]? downstreamTraceparents));
        string downstreamTraceparent = Assert.Single(downstreamTraceparents!);
        Assert.Matches($"^00-{responseTraceId}-[0-9a-f]{{16}}-01$", downstreamTraceparent);
    }

    [Fact]
    public async Task AggregateRouteDispatchesBothCatalogRequestsAndCombinesTheirResponses()
    {
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(Ct);
        Guid productId = Guid.NewGuid();
        const string productJson = "{\"id\":\"product-1\",\"name\":\"Tea\",\"price\":12.5}";
        const string propertiesJson = "{\"category\":\"Samsung\",\"model\":\"s10\"}";
        catalog.SetResponse(HttpMethod.Get, $"/api/v1/products/{productId}", HttpStatusCode.OK, productJson);
        catalog.SetResponse(HttpMethod.Get, $"/api/v1/products/{productId}/properties", HttpStatusCode.OK, propertiesJson);
        await using var factory = new GatewayApiFactory(catalog);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri($"/api/v1/products/{productId}/aggregate", UriKind.Relative), Ct);
        string responseJson = await response.Content.ReadAsStringAsync(Ct);

        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Unexpected response {(int)response.StatusCode}: {responseJson}; downstream requests: {string.Join(" | ", catalog.Requests.Select(request => $"{request.Method} {request.Path}"))}");
        using JsonDocument document = JsonDocument.Parse(responseJson);
        Assert.Equal(productJson, document.RootElement.GetProperty("product").GetRawText());
        Assert.Equal("Samsung", document.RootElement.GetProperty("properties").GetProperty("category").GetString());
        Assert.Equal("s10", document.RootElement.GetProperty("properties").GetProperty("model").GetString());
        Assert.Contains(catalog.Requests, request => request.Path == $"/api/v1/products/{productId}");
        Assert.Contains(catalog.Requests, request => request.Path == $"/api/v1/products/{productId}/properties");
        Assert.Equal(2, catalog.TotalRequestCount);
    }

    [Fact]
    public async Task AggregateMapsOcelotDownstreamTimeoutToGatewayTimeout()
    {
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(Ct);
        Guid productId = Guid.NewGuid();
        catalog.SetResponse(HttpMethod.Get, $"/api/v1/products/{productId}", HttpStatusCode.OK, "{}", TimeSpan.FromSeconds(4));
        catalog.SetResponse(HttpMethod.Get, $"/api/v1/products/{productId}/properties", HttpStatusCode.OK, "{}");
        await using var factory = new GatewayApiFactory(
            catalog,
            configurationOverrides: new Dictionary<string, string?>
            {
                ["Gateway:AggregateTimeoutSeconds"] = "1",
            });
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri($"/api/v1/products/{productId}/aggregate", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.DoesNotContain("127.0.0.1", await response.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        Assert.Contains(catalog.Requests, request => request.Path == $"/api/v1/products/{productId}");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AggregateReturnsNotFoundWhenEitherCatalogResourceIsMissing(bool productMissing)
    {
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(Ct);
        Guid productId = Guid.NewGuid();
        catalog.SetResponse(
            HttpMethod.Get,
            $"/api/v1/products/{productId}",
            productMissing ? HttpStatusCode.NotFound : HttpStatusCode.OK,
            productMissing ? "{}" : "{\"id\":\"product-1\"}");
        catalog.SetResponse(
            HttpMethod.Get,
            $"/api/v1/products/{productId}/properties",
            productMissing ? HttpStatusCode.OK : HttpStatusCode.NotFound,
            productMissing ? "{\"model\":\"s10\"}" : "{}");
        await using var factory = new GatewayApiFactory(catalog);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri($"/api/v1/products/{productId}/aggregate", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(2, catalog.TotalRequestCount);
    }

    [Fact]
    public async Task AggregateMapsMalformedDownstreamPayloadToBadGateway()
    {
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(Ct);
        Guid productId = Guid.NewGuid();
        catalog.SetResponse(HttpMethod.Get, $"/api/v1/products/{productId}", HttpStatusCode.OK, "not-json");
        catalog.SetResponse(HttpMethod.Get, $"/api/v1/products/{productId}/properties", HttpStatusCode.OK, "{\"model\":\"s10\"}");
        await using var factory = new GatewayApiFactory(catalog);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage response = await client.GetAsync(new Uri($"/api/v1/products/{productId}/aggregate", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task AggregateMapsUnreachableDownstreamToBadGateway()
    {
        await using DownstreamStub unusedCatalog = await DownstreamStub.StartAsync(Ct);
        await using var factory = new GatewayApiFactory(
            unusedCatalog,
            configurationOverrides: new Dictionary<string, string?>
            {
                ["Downstream:Catalog:BaseUrl"] = "http://127.0.0.1:1",
            });
        using HttpClient client = factory.CreateClient();
        Guid productId = Guid.NewGuid();

        using HttpResponseMessage response = await client.GetAsync(new Uri($"/api/v1/products/{productId}/aggregate", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task AggregateDispatchesBothDownstreamCallsBeforeEitherResponseIsReleased()
    {
        await using DownstreamStub unusedCatalog = await DownstreamStub.StartAsync(Ct);
        await using ConcurrentDownstreamServer catalog = await ConcurrentDownstreamServer.StartAsync(Ct);
        await using var factory = new GatewayApiFactory(
            unusedCatalog,
            configurationOverrides: new Dictionary<string, string?>
            {
                ["Downstream:Catalog:BaseUrl"] = catalog.BaseAddress.ToString(),
            });
        using HttpClient client = factory.CreateClient();
        Guid productId = Guid.NewGuid();

        using HttpResponseMessage response = await client.GetAsync(new Uri($"/api/v1/products/{productId}/aggregate", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, catalog.Requests.Length);
        Assert.Contains($"/api/v1/products/{productId}", catalog.Requests);
        Assert.Contains($"/api/v1/products/{productId}/properties", catalog.Requests);
    }

    [Fact]
    public async Task SwaggerUiAndAllowlistedDocumentsAreServedThroughGateway()
    {
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(Ct);
        await using DownstreamStub cart = await DownstreamStub.StartAsync(Ct);
        catalog.SetResponse(HttpMethod.Get, "/swagger/v1/swagger.json", HttpStatusCode.OK, CatalogSwaggerDocument);
        cart.SetResponse(HttpMethod.Get, "/swagger/v1/swagger.json", HttpStatusCode.OK, CartSwaggerDocument);
        cart.SetResponse(HttpMethod.Get, "/swagger/v2/swagger.json", HttpStatusCode.OK, CartSwaggerDocument.Replace("v1", "v2", StringComparison.Ordinal));
        await using var factory = new GatewayApiFactory(catalog, cart);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using HttpResponseMessage ui = await client.GetAsync(new Uri("/swagger", UriKind.Relative), Ct);
        using HttpResponseMessage uiIndex = await client.GetAsync(new Uri("/swagger/index.html", UriKind.Relative), Ct);
        string uiHtml = await uiIndex.Content.ReadAsStringAsync(Ct);
        using HttpResponseMessage documentUrls = await client.GetAsync(new Uri("/swagger/documentUrls", UriKind.Relative), Ct);
        using JsonDocument documentUrlList = JsonDocument.Parse(await documentUrls.Content.ReadAsStringAsync(Ct));
        using HttpResponseMessage catalogDocument = await client.GetAsync(new Uri("/swagger/catalog/v1/swagger.json", UriKind.Relative), Ct);
        using HttpResponseMessage cartV1Document = await client.GetAsync(new Uri("/swagger/cart/v1/swagger.json", UriKind.Relative), Ct);
        using HttpResponseMessage cartV2Document = await client.GetAsync(new Uri("/swagger/cart/v2/swagger.json", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.MovedPermanently, ui.StatusCode);
        Assert.Equal("swagger/index.html", ui.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, uiIndex.StatusCode);
        Assert.Contains("swagger-ui", uiHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, documentUrls.StatusCode);
        Assert.Equal(
            ExpectedSwaggerDocumentNames,
            documentUrlList.RootElement.EnumerateArray().Select(item => item.GetProperty("name").GetString()!).ToArray());
        Assert.Equal(
            ExpectedSwaggerDocumentUrls,
            documentUrlList.RootElement.EnumerateArray().Select(item => item.GetProperty("url").GetString()!).ToArray());
        Assert.Equal(HttpStatusCode.OK, catalogDocument.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cartV1Document.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cartV2Document.StatusCode);
        using JsonDocument catalogJson = JsonDocument.Parse(await catalogDocument.Content.ReadAsStringAsync(Ct));
        Assert.Equal("/", catalogJson.RootElement.GetProperty("servers")[0].GetProperty("url").GetString());
        Assert.True(catalogJson.RootElement.GetProperty("paths").TryGetProperty("/api/v1/products/{id}/aggregate", out _));
        Assert.Equal(1, catalog.GetRequestCount(HttpMethod.Get, "/swagger/v1/swagger.json"));
        Assert.Equal(1, cart.GetRequestCount(HttpMethod.Get, "/swagger/v1/swagger.json"));
        Assert.Equal(1, cart.GetRequestCount(HttpMethod.Get, "/swagger/v2/swagger.json"));
    }

    [Fact]
    public async Task SwaggerDocumentFailuresAreSafeAndDoNotBreakNormalProxyRoutes()
    {
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(Ct);
        catalog.SetResponse(HttpMethod.Get, "/swagger/v1/swagger.json", HttpStatusCode.BadGateway, "upstream internal failure");
        catalog.SetResponse(HttpMethod.Get, "/api/v1/categories", HttpStatusCode.Accepted, "[]");
        await using var factory = new GatewayApiFactory(catalog);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage unknown = await client.GetAsync(new Uri("/swagger/unknown/v1/swagger.json", UriKind.Relative), Ct);
        using HttpResponseMessage unavailable = await client.GetAsync(new Uri("/swagger/catalog/v1/swagger.json", UriKind.Relative), Ct);
        string unavailableBody = await unavailable.Content.ReadAsStringAsync(Ct);
        using HttpResponseMessage proxy = await client.GetAsync(new Uri("/api/v1/categories", UriKind.Relative), Ct);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadGateway, unavailable.StatusCode);
        Assert.DoesNotContain("upstream internal failure", unavailableBody, StringComparison.Ordinal);
        Assert.DoesNotContain(catalog.BaseAddress.Host, unavailableBody, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Accepted, proxy.StatusCode);
        Assert.Equal(1, catalog.GetRequestCount(HttpMethod.Get, "/api/v1/categories"));
    }

    private const string CatalogSwaggerDocument = """
        {
          "openapi": "3.0.1",
          "info": { "title": "Catalog", "version": "v1" },
          "paths": {
            "/api/v1/products/{id}": {
              "get": {
                "responses": {
                  "200": {
                    "description": "Product",
                    "content": { "application/json": { "schema": { "type": "object", "properties": { "id": { "type": "string" } } } } }
                  }
                }
              }
            }
          }
        }
        """;

    private const string CartSwaggerDocument = """
        {
          "openapi": "3.0.1",
          "info": { "title": "Cart", "version": "v1" },
          "paths": {}
        }
        """;

    private sealed class ConcurrentDownstreamServer(WebApplication application, Uri baseAddress, ConcurrentQueue<string> requests) : IAsyncDisposable
    {
        private readonly WebApplication _application = application;

        public Uri BaseAddress { get; } = baseAddress;

        public string[] Requests => requests.ToArray();

        public static async Task<ConcurrentDownstreamServer> StartAsync(CancellationToken cancellationToken)
        {
            var requests = new ConcurrentQueue<string>();
            var bothRequestsStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int requestCount = 0;
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.ConfigureKestrel(options => options.Listen(System.Net.IPAddress.Loopback, 0));

            WebApplication application = builder.Build();
            application.Run(async context =>
            {
                string path = context.Request.Path.Value ?? "/";
                requests.Enqueue(path);
                if (Interlocked.Increment(ref requestCount) == 2)
                {
                    bothRequestsStarted.TrySetResult();
                }

                try
                {
                    await bothRequestsStarted.Task.WaitAsync(context.RequestAborted).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
                {
                    return;
                }

                context.Response.ContentType = "application/json";
                context.Response.StatusCode = StatusCodes.Status200OK;
                await context.Response.WriteAsync(
                    path.EndsWith("/properties", StringComparison.Ordinal)
                        ? "{\"model\":\"s10\"}"
                        : "{\"id\":\"product-1\"}",
                    context.RequestAborted).ConfigureAwait(false);
            });

            await application.StartAsync(cancellationToken).ConfigureAwait(false);
            IServer server = application.Services.GetRequiredService<IServer>();
            string address = server.Features.Get<IServerAddressesFeature>()?.Addresses.Single()
                ?? throw new InvalidOperationException("The concurrency test server did not expose its loopback address.");
            return new ConcurrentDownstreamServer(application, new Uri(address), requests);
        }

        public async ValueTask DisposeAsync() => await _application.DisposeAsync().ConfigureAwait(false);
    }

}