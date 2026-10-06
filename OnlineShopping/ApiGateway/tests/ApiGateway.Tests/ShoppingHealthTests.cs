using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using ShoppingTelemetry;

namespace ApiGateway.Tests;

public sealed class ShoppingHealthTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound, HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable)]
    public async Task GatewayReadinessChecksDownstreamsWithoutAffectingLiveness(HttpStatusCode catalogStatus, HttpStatusCode cartStatus, HttpStatusCode expected)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using DownstreamStub catalog = await DownstreamStub.StartAsync(token);
        await using DownstreamStub cart = await DownstreamStub.StartAsync(token);
        catalog.SetResponse(HttpMethod.Get, "/health/ready", catalogStatus, "sensitive dependency details");
        cart.SetResponse(HttpMethod.Get, "/health/ready", cartStatus, "sensitive dependency details");
        await using var factory = new GatewayApiFactory(catalog, cart);
        using HttpClient client = factory.CreateClient();

        using HttpResponseMessage live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(0, catalog.TotalRequestCount);
        Assert.Equal(0, cart.TotalRequestCount);

        using HttpResponseMessage ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), token);
        Assert.Equal(expected, ready.StatusCode);
        Assert.Equal(expected == HttpStatusCode.OK ? "Healthy" : "Unhealthy", await ready.Content.ReadAsStringAsync(token));
        Assert.Equal(1, catalog.GetRequestCount(HttpMethod.Get, "/health/ready"));
        Assert.Equal(1, cart.GetRequestCount(HttpMethod.Get, "/health/ready"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DependencyProbeReportsStatusWithoutExposingExceptions(bool fails)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHealthChecks().AddDependencyCheck("database", (_, token) =>
        {
            token.ThrowIfCancellationRequested();
            return fails ? Task.FromException(new InvalidOperationException("secret connection details")) : Task.CompletedTask;
        });
        await using ServiceProvider provider = services.BuildServiceProvider();

        HealthReport report = await provider.GetRequiredService<HealthCheckService>().CheckHealthAsync(TestContext.Current.CancellationToken);

        Assert.Equal(fails ? HealthStatus.Unhealthy : HealthStatus.Healthy, report.Status);
        Assert.Null(report.Entries["database"].Exception);
        Assert.DoesNotContain("secret", report.Entries["database"].Description ?? string.Empty, StringComparison.Ordinal);
    }
}