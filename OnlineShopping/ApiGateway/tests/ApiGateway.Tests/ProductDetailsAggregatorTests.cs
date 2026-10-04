using System.Net;
using System.Text;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Ocelot.Configuration.Builder;
using Ocelot.Errors;
using Ocelot.Middleware;
using Ocelot.Requester;

using ApiGateway.Api;

namespace ApiGateway.Tests;

public sealed class ProductDetailsAggregatorTests : IDisposable
{
    private const string ProductRouteKey = "catalog-product-get";
    private const string PropertiesRouteKey = "catalog-product-properties";
    private readonly List<DownstreamResponse> _inputResponses = [];

    [Fact]
    public async Task ComposesPayloadsByRouteKeyRegardlessOfContextOrderAndKeepsContentAlive()
    {
        const string productJson = "{\"id\":\"product-1\",\"name\":\"Tea\",\"price\":12.5}";
        const string propertiesJson = "{\"category\":\"Samsung\",\"model\":\"s10\"}";
        var aggregator = new ProductDetailsAggregator();

        using DownstreamResponse result = await aggregator.Aggregate(
        [
            CreateContext(PropertiesRouteKey, HttpStatusCode.OK, propertiesJson),
            CreateContext(ProductRouteKey, HttpStatusCode.OK, productJson),
        ]);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal("application/json", result.Content.Headers.ContentType?.MediaType);
        using JsonDocument document = JsonDocument.Parse(await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(productJson, document.RootElement.GetProperty("product").GetRawText());
        Assert.Equal("Samsung", document.RootElement.GetProperty("properties").GetProperty("category").GetString());
        Assert.Equal("s10", document.RootElement.GetProperty("properties").GetProperty("model").GetString());
    }

    [Theory]
    [InlineData(ProductRouteKey)]
    [InlineData(PropertiesRouteKey)]
    public async Task ReturnsNotFoundWhenEitherRequiredRouteReturnsNotFound(string notFoundRouteKey)
    {
        using DownstreamResponse result = await new ProductDetailsAggregator().Aggregate(
        [
            CreateContext(ProductRouteKey, notFoundRouteKey == ProductRouteKey ? HttpStatusCode.NotFound : HttpStatusCode.OK, "{}"),
            CreateContext(PropertiesRouteKey, notFoundRouteKey == PropertiesRouteKey ? HttpStatusCode.NotFound : HttpStatusCode.OK, "{}"),
        ]);

        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    [Fact]
    public async Task ReturnsBadGatewayWhenRequiredResponseIsAbsent()
    {
        using DownstreamResponse result = await new ProductDetailsAggregator().Aggregate(
        [
            CreateContext(ProductRouteKey, HttpStatusCode.OK, "{}"),
            CreateContext(PropertiesRouteKey, null),
        ]);

        Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
    }

    [Theory]
    [InlineData("not-json", "{}")]
    [InlineData("[]", "{}")]
    [InlineData("{}", "[]")]
    [InlineData("{}", "{\"model\":42}")]
    public async Task ReturnsBadGatewayForMalformedOrUnexpectedPayloads(string productJson, string propertiesJson)
    {
        using DownstreamResponse result = await new ProductDetailsAggregator().Aggregate(
        [
            CreateContext(ProductRouteKey, HttpStatusCode.OK, productJson),
            CreateContext(PropertiesRouteKey, HttpStatusCode.OK, propertiesJson),
        ]);

        Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
    }

    [Fact]
    public async Task ReturnsBadGatewayForUnexpectedDownstreamStatus()
    {
        using DownstreamResponse result = await new ProductDetailsAggregator().Aggregate(
        [
            CreateContext(ProductRouteKey, HttpStatusCode.InternalServerError, "{}"),
            CreateContext(PropertiesRouteKey, HttpStatusCode.OK, "{}"),
        ]);

        Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
    }

    [Fact]
    public async Task ReturnsBadGatewayForTransportFailureError()
    {
        HttpContext failedContext = CreateContext(ProductRouteKey, null);
        failedContext.Items.UpsertErrors([new UnableToCompleteRequestError(new HttpRequestException("Connection failed."))]);

        using DownstreamResponse result = await new ProductDetailsAggregator().Aggregate(
        [
            failedContext,
            CreateContext(PropertiesRouteKey, HttpStatusCode.OK, "{}"),
        ]);

        Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
    }

    [Fact]
    public async Task ReturnsBadGatewayWhenNonNotFoundFailureAccompaniesNotFound()
    {
        using DownstreamResponse result = await new ProductDetailsAggregator().Aggregate(
        [
            CreateContext(ProductRouteKey, HttpStatusCode.NotFound),
            CreateContext(PropertiesRouteKey, HttpStatusCode.ServiceUnavailable),
        ]);

        Assert.Equal(HttpStatusCode.BadGateway, result.StatusCode);
    }

    [Fact]
    public async Task ReturnsGatewayTimeoutForIdentifiableTimeoutError()
    {
        HttpContext timedOutContext = CreateContext(ProductRouteKey, null);
        timedOutContext.Items.UpsertErrors([new RequestTimedOutError(new TimeoutException("The downstream request timed out."))]);

        using DownstreamResponse result = await new ProductDetailsAggregator().Aggregate(
        [
            timedOutContext,
            CreateContext(PropertiesRouteKey, HttpStatusCode.OK, "{}"),
        ]);

        Assert.Equal(HttpStatusCode.GatewayTimeout, result.StatusCode);
    }

    private DefaultHttpContext CreateContext(string routeKey, HttpStatusCode? statusCode, string body = "")
    {
        var context = new DefaultHttpContext();
        context.Items.UpsertDownstreamRoute(new DownstreamRouteBuilder().WithKey(routeKey).Build());

        if (statusCode is not null)
        {
            DownstreamResponse response = CreateDownstreamResponse(statusCode.Value, body);
            _inputResponses.Add(response);
            context.Items.UpsertDownstreamResponse(response);
        }

        return context;
    }

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The test retains and disposes each DownstreamResponse after its aggregate call.")]
    private static DownstreamResponse CreateDownstreamResponse(HttpStatusCode statusCode, string body)
    {
        var content = new StringContent(body, Encoding.UTF8, "application/json");
        return new DownstreamResponse(
            content,
            statusCode,
            new List<KeyValuePair<string, IEnumerable<string>>>(),
            statusCode.ToString());
    }

    public void Dispose()
    {
        foreach (DownstreamResponse response in _inputResponses)
        {
            response.Dispose();
        }
    }
}