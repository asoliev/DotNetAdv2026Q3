using Microsoft.AspNetCore.Mvc.Testing;

namespace IdentityService.Tests;

public class StartupSmokeTests
{
    [Fact]
    public async Task SwaggerJson_IsServedWhenTheApplicationStarts()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(new Uri("/swagger/v1/swagger.json", UriKind.Relative), TestContext.Current.CancellationToken);
        var payload = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        Assert.Contains("Identity Service API", payload, StringComparison.Ordinal);
    }
}
