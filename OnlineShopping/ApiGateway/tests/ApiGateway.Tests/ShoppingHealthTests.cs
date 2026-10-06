using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using ShoppingTelemetry;

namespace ApiGateway.Tests;

public sealed class ShoppingHealthTests
{
    [Theory]
    [InlineData("Development", "127.0.0.1", null, true)]
    [InlineData("Development", "::1", null, true)]
    [InlineData("Development", "192.0.2.1", null, false)]
    [InlineData("Production", "127.0.0.1", null, false)]
    [InlineData("Production", "127.0.0.1", "Manager", false)]
    [InlineData("Production", "192.0.2.1", "admin", true)]
    [InlineData("Production", "192.0.2.1", "Operations", true)]
    public async Task DetailsPolicyRestrictsAccess(string environment, string address, string? role, bool allowed)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = environment });
        builder.AddShoppingHealthChecks();
        await using WebApplication application = builder.Build();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        var identity = role is null ? new ClaimsIdentity() : new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test");
        AuthorizationResult result = await application.Services.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(new ClaimsPrincipal(identity), context, "HealthDetails");
        Assert.Equal(allowed, result.Succeeded);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "Healthy")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "Unhealthy")]
    public async Task DetailsReturnsSanitizedJsonForAdministrators(HttpStatusCode downstreamStatus, string expectedStatus)
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await using DownstreamStub downstream = await DownstreamStub.StartAsync(token);
        downstream.SetResponse(HttpMethod.Get, "/health/ready", downstreamStatus, "sensitive dependency details");
        await using var factory = new GatewayApiFactory(downstream);
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage anonymous = await client.GetAsync(new Uri("/health/details", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Manager);
        using HttpResponseMessage forbidden = await client.GetAsync(new Uri("/health/details", UriKind.Relative), token);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Admin);
        using HttpResponseMessage response = await client.GetAsync(new Uri("/health/details", UriKind.Relative), token);
        Assert.Equal(downstreamStatus, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        string body = await response.Content.ReadAsStringAsync(token);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Equal(expectedStatus, document.RootElement.GetProperty("status").GetString());
        Assert.Equal(expectedStatus, document.RootElement.GetProperty("checks").GetProperty("Catalog").GetProperty("status").GetString());
        Assert.Equal("Healthy", document.RootElement.GetProperty("checks").GetProperty("self").GetProperty("status").GetString());
        Assert.DoesNotContain("sensitive", body, StringComparison.Ordinal);
        Assert.DoesNotContain("exception", body, StringComparison.OrdinalIgnoreCase);
    }

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