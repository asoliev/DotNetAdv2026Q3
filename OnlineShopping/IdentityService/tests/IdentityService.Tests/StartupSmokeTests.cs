using Microsoft.AspNetCore.Mvc.Testing;

namespace IdentityService.Tests;

public class StartupSmokeTests
{
    [Fact]
    public async Task SwaggerJson_IsServedWhenTheApplicationStarts()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync("/swagger/v1/swagger.json");
        string payload = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Identity Service API", payload);
    }
}
