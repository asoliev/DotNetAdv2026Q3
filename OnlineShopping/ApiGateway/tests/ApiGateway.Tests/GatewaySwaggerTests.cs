using System.Net;
using System.Text;
using System.Text.Json.Nodes;

using ApiGateway.Api;
using Microsoft.Extensions.Configuration;

namespace ApiGateway.Tests;

public sealed class GatewaySwaggerTests
{
  private static readonly string[] CartMethods = ["get", "post", "delete"];
  private static readonly string[] CartRoles = ["Manager", "Store customer"];

    private const string CatalogDocument = """
        {
          "openapi": "3.0.1",
          "info": { "title": "Catalog API", "version": "v1" },
          "servers": [{ "url": "http://catalog.internal" }],
          "paths": {
            "/api/v1/products/{id}": {
              "servers": [{ "url": "http://path.catalog.internal" }],
              "parameters": [{ "name": "id", "in": "path", "required": true, "schema": { "type": "string" } }],
              "get": {
                "servers": [{ "url": "http://operation.catalog.internal" }],
                "operationId": "getProduct",
                "responses": {
                  "200": {
                    "description": "Product",
                    "content": { "application/json": { "schema": { "$ref": "#/components/schemas/ProductModelWithAnUnpredictableName" } } }
                  }
                }
              },
              "put": { "responses": { "204": { "description": "Updated" } } },
              "delete": { "responses": { "204": { "description": "Deleted" } } }
            },
            "/api/v1/products/{id}/properties": {
              "get": {
                "responses": {
                  "200": {
                    "description": "Properties",
                    "content": { "application/json": { "schema": { "type": "object", "additionalProperties": { "type": "string" } } } }
                  }
                }
              }
            },
            "/api/v1/products": {
              "get": { "responses": { "200": { "description": "Product page" } } },
              "post": { "responses": { "201": { "description": "Created" } } }
            },
            "/api/v1/categories": {
              "get": { "responses": { "200": { "description": "Categories" } } },
              "post": { "responses": { "201": { "description": "Created" } } }
            }
          },
          "components": {
            "schemas": {
              "ProductModelWithAnUnpredictableName": {
                "type": "object",
                "properties": { "sku": { "type": "string" }, "price": { "type": "number" } }
              }
            }
          }
        }
        """;

    private const string CartDocument = """
        {
          "openapi": "3.0.1",
          "info": { "title": "Cart API", "version": "v1" },
          "servers": [{ "url": "http://cart.internal" }],
          "paths": {
            "/api/v1/carts/{cartKey}": {
              "servers": [{ "url": "http://path.cart.internal" }],
              "get": {
                "servers": [{ "url": "http://operation.cart.internal" }],
                "responses": { "200": { "description": "Cart" } }
              }
            },
            "/api/v1/carts/{cartKey}/items": {
              "post": { "responses": { "200": { "description": "Updated" } } },
              "delete": { "responses": { "200": { "description": "Deleted" } } }
            }
          }
        }
        """;

    [Fact]
    public async Task CatalogDocumentGetsGatewayServersSecurityAndAggregateSchema()
    {
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, CatalogDocument)));
        using var client = new HttpClient(handler, disposeHandler: false);
        GatewaySwaggerDocumentProvider provider = CreateProvider(client);

        JsonObject document = await provider.GetDocumentAsync("catalog", "v1", TestContext.Current.CancellationToken);

        Assert.Equal(new Uri("http://catalog.internal/swagger/v1/swagger.json"), Assert.Single(handler.RequestedUris));
        Assert.Equal("/", Assert.Single(document["servers"]!.AsArray())!["url"]!.GetValue<string>());
        AssertBearerScheme(document);

        JsonObject paths = document["paths"]!.AsObject();
        Assert.Equal(5, paths.Count);
        Assert.NotNull(paths["/api/v1/products/{id}"]!["parameters"]);
        Assert.Equal("getProduct", paths["/api/v1/products/{id}"]!["get"]!["operationId"]!.GetValue<string>());
        Assert.Null(paths["/api/v1/products/{id}"]!["servers"]);
        Assert.Null(paths["/api/v1/products/{id}"]!["get"]!["servers"]);
        Assert.Equal(
          "string",
          paths["/api/v1/products/{id}/properties"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["additionalProperties"]!["type"]!.GetValue<string>());
        Assert.Equal(
          "#/components/schemas/ProductModelWithAnUnpredictableName",
          paths["/api/v1/products/{id}"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!["$ref"]!.GetValue<string>());
        Assert.Null(paths["/api/v1/products/{id}"]!["patch"]);
        Assert.Empty(paths["/api/v1/products/{id}"]!["get"]!["security"]!.AsArray());
        Assert.Equal("Bearer", paths["/api/v1/products"]!["post"]!["security"]![0]!.AsObject().First().Key);
        Assert.Equal("admin", paths["/api/v1/products"]!["post"]!["x-required-roles"]![0]!.GetValue<string>());
        Assert.Equal("admin", paths["/api/v1/products/{id}"]!["put"]!["x-required-roles"]![0]!.GetValue<string>());
        Assert.Equal("admin", paths["/api/v1/products/{id}"]!["delete"]!["x-required-roles"]![0]!.GetValue<string>());
        Assert.Empty(paths["/api/v1/categories"]!["get"]!["security"]!.AsArray());
        Assert.Equal("admin", paths["/api/v1/categories"]!["post"]!["x-required-roles"]![0]!.GetValue<string>());

        JsonObject aggregateGet = paths["/api/v1/products/{id}/aggregate"]!["get"]!.AsObject();
        Assert.Empty(aggregateGet["security"]!.AsArray());
        JsonObject aggregateResponse = aggregateGet["responses"]!.AsObject();
        Assert.True(aggregateResponse.ContainsKey("200"));
        Assert.True(aggregateResponse.ContainsKey("404"));
        Assert.True(aggregateResponse.ContainsKey("502"));
        Assert.True(aggregateResponse.ContainsKey("504"));
        JsonObject responseSchema = aggregateResponse["200"]!["content"]!["application/json"]!["schema"]!.AsObject();
        Assert.Equal("object", responseSchema["type"]!.GetValue<string>());
        JsonObject productSchema = responseSchema["properties"]!["product"]!.AsObject();
        Assert.Equal("string", productSchema["properties"]!["sku"]!["type"]!.GetValue<string>());
        Assert.Equal("object", responseSchema["properties"]!["properties"]!["type"]!.GetValue<string>());
        Assert.Equal("string", responseSchema["properties"]!["properties"]!["additionalProperties"]!["type"]!.GetValue<string>());
        Assert.DoesNotContain("catalog.internal", document.ToJsonString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("v1")]
    [InlineData("v2")]
    public async Task CartDocumentsRequireBearerAndDescribeExistingRoles(string version)
    {
        string payload = version == "v1"
            ? CartDocument
            : CartDocument.Replace("/api/v1/", "/api/v2/", StringComparison.Ordinal);
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, payload)));
        using var client = new HttpClient(handler, disposeHandler: false);
        GatewaySwaggerDocumentProvider provider = CreateProvider(client);

        JsonObject document = await provider.GetDocumentAsync("cart", version, TestContext.Current.CancellationToken);

        Assert.Equal(new Uri($"http://cart.internal/swagger/{version}/swagger.json"), Assert.Single(handler.RequestedUris));
        Assert.Equal("/", Assert.Single(document["servers"]!.AsArray())!["url"]!.GetValue<string>());
        AssertBearerScheme(document);
        foreach (JsonObject pathItem in document["paths"]!.AsObject().Select(path => path.Value!.AsObject()))
        {
            Assert.Null(pathItem["servers"]);
            foreach (string method in CartMethods.Where(pathItem.ContainsKey))
            {
                JsonObject operation = pathItem[method]!.AsObject();
                Assert.Null(operation["servers"]);
                Assert.Equal("Bearer", operation["security"]![0]!.AsObject().First().Key);
                Assert.Equal(CartRoles, operation["x-required-roles"]!.AsArray().Select(role => role!.GetValue<string>()).ToArray());
                Assert.Equal("any", operation["x-role-match"]!.GetValue<string>());
            }
        }
    }

    [Theory]
    [InlineData("catalog", "v2")]
    [InlineData("cart", "v3")]
    [InlineData("other", "v1")]
    public async Task UnknownSelectionThrowsNotFoundWithoutFetching(string service, string version)
    {
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{}")));
        using var client = new HttpClient(handler, disposeHandler: false);
        GatewaySwaggerDocumentProvider provider = CreateProvider(client);

        await Assert.ThrowsAsync<GatewaySwaggerDocumentNotFoundException>(
            () => provider.GetDocumentAsync(service, version, TestContext.Current.CancellationToken));

        Assert.Empty(handler.RequestedUris);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadGateway, "{}")]
    [InlineData(HttpStatusCode.OK, "not-json")]
    [InlineData(HttpStatusCode.OK, "{\"openapi\":\"3.0.1\",\"paths\":[]}")]
    public async Task UpstreamOrInvalidDocumentUsesSafeFailureContract(HttpStatusCode statusCode, string payload)
    {
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(statusCode, payload)));
        using var client = new HttpClient(handler, disposeHandler: false);
        GatewaySwaggerDocumentProvider provider = CreateProvider(client);

        GatewaySwaggerDocumentException exception = await Assert.ThrowsAsync<GatewaySwaggerDocumentException>(
            () => provider.GetDocumentAsync("catalog", "v1", TestContext.Current.CancellationToken));

        Assert.Equal("The upstream Swagger document could not be retrieved or parsed.", exception.Message);
        Assert.DoesNotContain("catalog.internal", exception.Message, StringComparison.Ordinal);
        Assert.Single(handler.RequestedUris);
    }

    [Fact]
    public async Task InvalidLocalProductSchemaReferenceUsesSafeFailureContract()
    {
        string invalidSchema = CatalogDocument.Replace(
            "#/components/schemas/ProductModelWithAnUnpredictableName",
            "#/components/schemas/DoesNotExist",
            StringComparison.Ordinal);
        using var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, invalidSchema)));
        using var client = new HttpClient(handler, disposeHandler: false);
        GatewaySwaggerDocumentProvider provider = CreateProvider(client);

        await Assert.ThrowsAsync<GatewaySwaggerDocumentException>(
            () => provider.GetDocumentAsync("catalog", "v1", TestContext.Current.CancellationToken));
    }

      [Fact]
      public async Task PropagatesCancellationToUpstreamRequest()
      {
        var requestStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
          requestStarted.TrySetResult(true);
          await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
          return JsonResponse(HttpStatusCode.OK, CatalogDocument);
        });
        using var client = new HttpClient(handler, disposeHandler: false);
        GatewaySwaggerDocumentProvider provider = CreateProvider(client);
        using var cancellationSource = new CancellationTokenSource();
        Task<JsonObject> pendingRequest = provider.GetDocumentAsync("catalog", "v1", cancellationSource.Token);

        await requestStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
        await cancellationSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pendingRequest);
      }

    private static GatewaySwaggerDocumentProvider CreateProvider(HttpClient client)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Downstream:Catalog:BaseUrl"] = "http://catalog.internal",
                ["Downstream:Cart:BaseUrl"] = "http://cart.internal",
            })
            .Build();
        return new GatewaySwaggerDocumentProvider(client, configuration);
    }

    private static void AssertBearerScheme(JsonObject document)
    {
        JsonObject bearer = document["components"]!["securitySchemes"]!["Bearer"]!.AsObject();
        Assert.Equal("http", bearer["type"]!.GetValue<string>());
        Assert.Equal("bearer", bearer["scheme"]!.GetValue<string>());
        Assert.Equal("JWT", bearer["bearerFormat"]!.GetValue<string>());
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string payload) => new(statusCode)
    {
        Content = new StringContent(payload, Encoding.UTF8, "application/json"),
    };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public List<Uri> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri!);
            return responder(request, cancellationToken);
        }
    }
}