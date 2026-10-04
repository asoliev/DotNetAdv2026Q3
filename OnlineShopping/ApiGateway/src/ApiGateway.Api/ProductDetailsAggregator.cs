using System.Net;
using System.Text;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

using Ocelot.Errors;
using Ocelot.Middleware;

namespace ApiGateway.Api;

public sealed class ProductDetailsAggregator : Ocelot.Multiplexer.IDefinedAggregator
{
    private const string ProductRouteKey = "catalog-product-get";
    private const string PropertiesRouteKey = "catalog-product-properties";

    public async Task<DownstreamResponse> Aggregate(List<HttpContext> responses)
    {
        if (responses is null)
        {
            return CreateResponse(HttpStatusCode.BadGateway);
        }

        var downstreamResponses = new Dictionary<string, DownstreamResponse>(StringComparer.Ordinal);
        foreach (HttpContext? context in responses)
        {
            if (context is null)
            {
                return CreateResponse(HttpStatusCode.BadGateway);
            }

            var errors = context.Items.Errors();
            if (errors.Any(error => error is RequestTimedOutError))
            {
                return CreateResponse(HttpStatusCode.GatewayTimeout);
            }

            if (errors.Count > 0)
            {
                return CreateResponse(HttpStatusCode.BadGateway);
            }

            string? routeKey = context.Items.DownstreamRoute()?.Key;
            if (routeKey is not ProductRouteKey and not PropertiesRouteKey)
            {
                return CreateResponse(HttpStatusCode.BadGateway);
            }

            DownstreamResponse? response = context.Items.DownstreamResponse();
            if (response is null || !downstreamResponses.TryAdd(routeKey, response))
            {
                return CreateResponse(HttpStatusCode.BadGateway);
            }
        }

        if (!downstreamResponses.TryGetValue(ProductRouteKey, out DownstreamResponse? productResponse)
            || !downstreamResponses.TryGetValue(PropertiesRouteKey, out DownstreamResponse? propertiesResponse))
        {
            return CreateResponse(HttpStatusCode.BadGateway);
        }

        if ((!IsSuccess(productResponse.StatusCode) && productResponse.StatusCode != HttpStatusCode.NotFound)
            || (!IsSuccess(propertiesResponse.StatusCode) && propertiesResponse.StatusCode != HttpStatusCode.NotFound))
        {
            return CreateResponse(HttpStatusCode.BadGateway);
        }

        if (productResponse.StatusCode == HttpStatusCode.NotFound || propertiesResponse.StatusCode == HttpStatusCode.NotFound)
        {
            return CreateResponse(HttpStatusCode.NotFound);
        }

        if (productResponse.Content is null || propertiesResponse.Content is null)
        {
            return CreateResponse(HttpStatusCode.BadGateway);
        }

        try
        {
            string productJson = await productResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            string propertiesJson = await propertiesResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            string? aggregateJson = ComposeJson(productJson, propertiesJson);

            return aggregateJson is null
                ? CreateResponse(HttpStatusCode.BadGateway)
                : CreateResponse(HttpStatusCode.OK, aggregateJson);
        }
        catch (Exception exception) when (exception is JsonException or IOException or InvalidOperationException)
        {
            return CreateResponse(HttpStatusCode.BadGateway);
        }
    }

    private static bool IsSuccess(HttpStatusCode statusCode) => (int)statusCode is >= 200 and < 300;

    private static string? ComposeJson(string productJson, string propertiesJson)
    {
        using JsonDocument product = JsonDocument.Parse(productJson);
        using JsonDocument properties = JsonDocument.Parse(propertiesJson);

        if (product.RootElement.ValueKind != JsonValueKind.Object
            || properties.RootElement.ValueKind != JsonValueKind.Object
            || properties.RootElement.EnumerateObject().Any(property => property.Value.ValueKind != JsonValueKind.String))
        {
            return null;
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WritePropertyName("product");
            product.RootElement.WriteTo(writer);
            writer.WritePropertyName("properties");
            properties.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The returned DownstreamResponse owns this content and is disposed by its caller.")]
    private static DownstreamResponse CreateResponse(HttpStatusCode statusCode, string body = "")
    {
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        return new DownstreamResponse(
            content,
            statusCode,
            new List<KeyValuePair<string, IEnumerable<string>>>(),
            statusCode.ToString());
    }
}