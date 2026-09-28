using CatalogService.Api.Messaging;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CatalogService.Tests.Api;

/// <summary>
/// Hosts the real CatalogService.Api in memory, backed by a throw-away SQLite database
/// and with the RabbitMQ publisher replaced by <see cref="RecordingProductEventPublisher"/>.
/// </summary>
public sealed class CatalogApiFactory : WebApplicationFactory<Program>
{
    private readonly DirectoryInfo _databaseDirectory = Directory.CreateTempSubdirectory("catalog-api-tests-");

    public RecordingProductEventPublisher Publisher { get; } = new();

    public string DatabasePath => Path.Combine(_databaseDirectory.FullName, "catalog.db");

    public HttpClient CreateClient(string? accessToken)
    {
        HttpClient client = CreateClient();
        if (accessToken is not null)
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting("Database:Directory", _databaseDirectory.FullName);
        builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<IProductEventPublisher>(Publisher)));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        SqliteConnection.ClearAllPools();
        _databaseDirectory.Delete(recursive: true);
    }
}
