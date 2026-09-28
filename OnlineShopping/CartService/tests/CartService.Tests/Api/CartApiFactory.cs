using CartService.Api.Messaging;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CartService.Tests.Api;

/// <summary>
/// Hosts the real CartService.Api in memory, backed by a throw-away LiteDB database.
/// The RabbitMQ consumer is removed so the tests don't need a running broker.
/// </summary>
public sealed class CartApiFactory : WebApplicationFactory<Program>
{
    private readonly DirectoryInfo _databaseDirectory = Directory.CreateTempSubdirectory("cart-api-tests-");

    public HttpClient CreateClient(string? authorization)
    {
        HttpClient client = CreateClient();
        if (authorization is not null)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authorization);
        }

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("Database:Directory", _databaseDirectory.FullName);
        builder.ConfigureServices(services => services.Remove(services.Single(descriptor => descriptor.ServiceType == typeof(IHostedService) && descriptor.ImplementationType == typeof(RabbitMqCatalogEventConsumer))));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _databaseDirectory.Delete(recursive: true);
    }
}
