using ApiGateway.Api;

using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiGateway.Tests;

public sealed class GatewayApiFactory : WebApplicationFactory<Program>
{
    internal static readonly object EnvironmentConfigurationLock = new();
    private readonly IReadOnlyDictionary<string, string?> _configurationOverrides;

    public GatewayApiFactory(
        DownstreamStub catalogStub,
        DownstreamStub? cartStub = null,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(catalogStub);
        CatalogStub = catalogStub;
        CartStub = cartStub ?? catalogStub;

        var overrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Downstream:Catalog:BaseUrl"] = CatalogStub.BaseAddress.ToString(),
            ["Downstream:Cart:BaseUrl"] = CartStub.BaseAddress.ToString(),
        };
        if (configurationOverrides is not null)
        {
            foreach ((string key, string? value) in configurationOverrides)
            {
                overrides[key] = value;
            }
        }

        _configurationOverrides = overrides;
    }

    public DownstreamStub CatalogStub { get; }

    public DownstreamStub CartStub { get; }

    public new HttpClient CreateClient() => CreateClient(new WebApplicationFactoryClientOptions());

    public new HttpClient CreateClient(WebApplicationFactoryClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        lock (EnvironmentConfigurationLock)
        {
            var environmentOverrides = _configurationOverrides.ToDictionary(
                pair => ToEnvironmentVariable(pair.Key),
                pair => Environment.GetEnvironmentVariable(ToEnvironmentVariable(pair.Key)),
                StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach ((string key, string? value) in _configurationOverrides)
                {
                    Environment.SetEnvironmentVariable(ToEnvironmentVariable(key), value);
                }

                return base.CreateClient(options);
            }
            finally
            {
                foreach ((string key, string? value) in environmentOverrides)
                {
                    Environment.SetEnvironmentVariable(key, value);
                }
            }
        }
    }

    private static string ToEnvironmentVariable(string configurationKey) => configurationKey.Replace(":", "__", StringComparison.Ordinal);
}