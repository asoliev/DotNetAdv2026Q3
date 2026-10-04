using System.Net;

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
}