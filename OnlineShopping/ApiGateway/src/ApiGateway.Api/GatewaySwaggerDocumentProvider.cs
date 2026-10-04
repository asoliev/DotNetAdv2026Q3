using System.Net;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ApiGateway.Api;

public sealed class GatewaySwaggerDocumentProvider
{
    private const string CatalogProductPath = "/api/v1/products/{id}";
    private const string AggregateProductPath = "/api/v1/products/{id}/aggregate";
    private static readonly string[] OperationMethods = ["get", "put", "post", "delete", "options", "head", "patch", "trace"];

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GatewaySwaggerDocumentProvider(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<JsonObject> GetDocumentAsync(string service, string version, CancellationToken cancellationToken)
    {
        string configuredService = ResolveService(service, version);
        Uri requestUri = ResolveRequestUri(configuredService, version);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException)
        {
            throw new GatewaySwaggerDocumentException();
        }

        using (response)
        {
            if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
            {
                throw new GatewaySwaggerDocumentException();
            }

            string payload;
            try
            {
                payload = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is HttpRequestException or IOException)
            {
                throw new GatewaySwaggerDocumentException();
            }

            JsonObject document;
            try
            {
                document = JsonNode.Parse(payload) as JsonObject ?? throw new GatewaySwaggerDocumentException();
            }
            catch (JsonException)
            {
                throw new GatewaySwaggerDocumentException();
            }

            if (document["paths"] is not JsonObject paths)
            {
                throw new GatewaySwaggerDocumentException();
            }

            try
            {
                EnsureBearerScheme(document);
                RemoveServiceServers(document, paths);
                ApplyOperationSecurity(paths, configuredService);
                document["servers"] = new JsonArray(new JsonObject { ["url"] = "/" });

                if (configuredService == "Catalog")
                {
                    AddProductAggregateOperation(document, paths);
                }
            }
            catch (GatewaySwaggerDocumentException)
            {
                throw;
            }
            catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
            {
                throw new GatewaySwaggerDocumentException();
            }

            return document;
        }
    }

    private static string ResolveService(string service, string version) => (service, version) switch
    {
        ("catalog", "v1") => "Catalog",
        ("cart", "v1") => "Cart",
        ("cart", "v2") => "Cart",
        _ => throw new GatewaySwaggerDocumentNotFoundException(),
    };

    private Uri ResolveRequestUri(string service, string version)
    {
        string configurationKey = $"Downstream:{service}:BaseUrl";
        string? configuredAddress = _configuration[configurationKey];
        if (!Uri.TryCreate(configuredAddress, UriKind.Absolute, out Uri? baseAddress)
            || (baseAddress.Scheme != Uri.UriSchemeHttp && baseAddress.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(baseAddress.UserInfo)
            || !string.IsNullOrEmpty(baseAddress.Query)
            || !string.IsNullOrEmpty(baseAddress.Fragment)
            || baseAddress.AbsolutePath != "/")
        {
            throw new GatewaySwaggerDocumentException();
        }

        return new Uri(baseAddress, $"swagger/{version}/swagger.json");
    }

    private static void EnsureBearerScheme(JsonObject document)
    {
        JsonObject components;
        if (document["components"] is null)
        {
            components = new JsonObject();
            document["components"] = components;
        }
        else if (document["components"] is JsonObject existingComponents)
        {
            components = existingComponents;
        }
        else
        {
            throw new GatewaySwaggerDocumentException();
        }

        JsonObject securitySchemes;
        if (components["securitySchemes"] is null)
        {
            securitySchemes = new JsonObject();
            components["securitySchemes"] = securitySchemes;
        }
        else if (components["securitySchemes"] is JsonObject existingSchemes)
        {
            securitySchemes = existingSchemes;
        }
        else
        {
            throw new GatewaySwaggerDocumentException();
        }

        securitySchemes["Bearer"] = new JsonObject
        {
            ["type"] = "http",
            ["scheme"] = "bearer",
            ["bearerFormat"] = "JWT",
        };
    }

    private static void RemoveServiceServers(JsonObject document, JsonObject paths)
    {
        document.Remove("servers");
        foreach (JsonNode? pathNode in paths.Select(path => path.Value))
        {
            if (pathNode is not JsonObject pathItem)
            {
                continue;
            }

            pathItem.Remove("servers");
            foreach (string method in OperationMethods)
            {
                if (pathItem[method] is JsonObject operation)
                {
                    operation.Remove("servers");
                }
            }
        }
    }

    private static void ApplyOperationSecurity(JsonObject paths, string service)
    {
        foreach (JsonNode? pathNode in paths.Select(path => path.Value))
        {
            if (pathNode is not JsonObject pathItem)
            {
                continue;
            }

            foreach (string method in OperationMethods)
            {
                if (pathItem[method] is not JsonObject operation)
                {
                    continue;
                }

                if (service == "Cart")
                {
                    SetBearerSecurity(operation);
                    operation["x-required-roles"] = CreateRoleList("Manager", "Store customer");
                    operation["x-role-match"] = "any";
                }
                else if (method == "get")
                {
                    operation["security"] = new JsonArray();
                    operation.Remove("x-required-roles");
                    operation.Remove("x-role-match");
                }
                else if (method is "post" or "put" or "delete")
                {
                    SetBearerSecurity(operation);
                    operation["x-required-roles"] = CreateRoleList("admin");
                    operation.Remove("x-role-match");
                }
            }
        }
    }

    private static void SetBearerSecurity(JsonObject operation)
    {
        operation["security"] = new JsonArray(new JsonObject { ["Bearer"] = new JsonArray() });
    }

    private static JsonArray CreateRoleList(params string[] roles) => new(roles.Select(role => (JsonNode?)JsonValue.Create(role)).ToArray());

    private static void AddProductAggregateOperation(JsonObject document, JsonObject paths)
    {
        if (paths[CatalogProductPath] is not JsonObject productPath
            || productPath["get"] is not JsonObject productGet
            || productGet["responses"] is not JsonObject productResponses
            || productResponses["200"] is not JsonObject productResponse
            || productResponse["content"] is not JsonObject productContent
            || productContent["application/json"] is not JsonObject productMediaType
            || productMediaType["schema"] is not JsonNode productSchemaNode)
        {
            throw new GatewaySwaggerDocumentException();
        }

        JsonNode productSchema = ResolveLocalReferences(productSchemaNode, document, new HashSet<string>(StringComparer.Ordinal));
        if (productSchema is not JsonObject)
        {
            throw new GatewaySwaggerDocumentException();
        }

        JsonObject aggregatePath;
        if (paths[AggregateProductPath] is null)
        {
            aggregatePath = new JsonObject();
            paths[AggregateProductPath] = aggregatePath;
        }
        else if (paths[AggregateProductPath] is JsonObject existingAggregatePath)
        {
            aggregatePath = existingAggregatePath;
        }
        else
        {
            throw new GatewaySwaggerDocumentException();
        }

        aggregatePath["get"] = new JsonObject
        {
            ["operationId"] = "GetAggregatedProductDetails",
            ["summary"] = "Returns a product with its properties.",
            ["security"] = new JsonArray(),
            ["responses"] = new JsonObject
            {
                ["200"] = new JsonObject
                {
                    ["description"] = "Product details and properties.",
                    ["content"] = new JsonObject
                    {
                        ["application/json"] = new JsonObject
                        {
                            ["schema"] = new JsonObject
                            {
                                ["type"] = "object",
                                ["required"] = CreateRoleList("product", "properties"),
                                ["properties"] = new JsonObject
                                {
                                    ["product"] = productSchema,
                                    ["properties"] = new JsonObject
                                    {
                                        ["type"] = "object",
                                        ["additionalProperties"] = new JsonObject { ["type"] = "string" },
                                    },
                                },
                            },
                        },
                    },
                },
                ["404"] = new JsonObject { ["description"] = "The product does not exist." },
                ["502"] = new JsonObject { ["description"] = "A required downstream response is unavailable or invalid." },
                ["504"] = new JsonObject { ["description"] = "A required downstream request timed out." },
            },
        };
    }

    private static JsonNode ResolveLocalReferences(JsonNode node, JsonObject document, HashSet<string> referenceStack)
    {
        if (node is JsonArray array)
        {
            var resolvedArray = new JsonArray();
            foreach (JsonNode? item in array)
            {
                resolvedArray.Add(item is null ? null : ResolveLocalReferences(item, document, referenceStack));
            }

            return resolvedArray;
        }

        if (node is not JsonObject jsonObject)
        {
            return node.DeepClone();
        }

        if (jsonObject["$ref"] is JsonValue referenceNode
            && referenceNode.TryGetValue(out string? reference)
            && reference is not null
            && (reference == "#" || reference.StartsWith("#/", StringComparison.Ordinal)))
        {
            if (!referenceStack.Add(reference))
            {
                return jsonObject.DeepClone();
            }

            JsonNode target = ResolveJsonPointer(document, reference) ?? throw new GatewaySwaggerDocumentException();
            JsonNode resolvedTarget = ResolveLocalReferences(target, document, referenceStack);
            referenceStack.Remove(reference);

            if (jsonObject.Count == 1)
            {
                return resolvedTarget;
            }

            if (resolvedTarget is not JsonObject resolvedObject)
            {
                throw new GatewaySwaggerDocumentException();
            }

            var merged = (JsonObject)resolvedObject.DeepClone();
            foreach ((string key, JsonNode? value) in jsonObject)
            {
                if (key != "$ref")
                {
                    merged[key] = value is null ? null : ResolveLocalReferences(value, document, referenceStack);
                }
            }

            return merged;
        }

        var resolvedObjectNode = new JsonObject();
        foreach ((string key, JsonNode? value) in jsonObject)
        {
            resolvedObjectNode[key] = value is null ? null : ResolveLocalReferences(value, document, referenceStack);
        }

        return resolvedObjectNode;
    }

    private static JsonNode? ResolveJsonPointer(JsonObject document, string reference)
    {
        JsonNode? current = document;
        if (reference == "#")
        {
            return current;
        }

        string[] segments = reference[2..].Split('/');
        foreach (string rawSegment in segments)
        {
            string segment = Uri.UnescapeDataString(rawSegment).Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
            if (current is JsonObject currentObject && currentObject.TryGetPropertyValue(segment, out JsonNode? property))
            {
                current = property;
            }
            else if (current is JsonArray currentArray
                && int.TryParse(segment, out int index)
                && index >= 0
                && index < currentArray.Count)
            {
                current = currentArray[index];
            }
            else
            {
                return null;
            }
        }

        return current;
    }
}

[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "The fixed public message prevents unknown selections from disclosing internal configuration.")]
public sealed class GatewaySwaggerDocumentNotFoundException : Exception
{
    public GatewaySwaggerDocumentNotFoundException() : base("The requested Swagger document is not available.")
    {
    }
}

[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "The fixed public message prevents upstream details from reaching the public response.")]
public sealed class GatewaySwaggerDocumentException : Exception
{
    public GatewaySwaggerDocumentException() : base("The upstream Swagger document could not be retrieved or parsed.")
    {
    }
}