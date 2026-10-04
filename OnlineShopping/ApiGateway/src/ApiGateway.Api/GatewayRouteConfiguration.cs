using System.Globalization;

namespace ApiGateway.Api;

public static class GatewayRouteConfiguration
{
    public static IConfigurationRoot Load(IConfiguration configuration, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        IConfigurationRoot routeConfiguration = new ConfigurationBuilder()
            .SetBasePath(contentRootPath)
            .AddJsonFile("ocelot.json", optional: false, reloadOnChange: false)
            .Build();

        var destinationOverrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (IConfigurationSection route in routeConfiguration.GetSection("Routes").GetChildren())
        {
            string routeKey = route["Key"] ?? throw new InvalidOperationException("Every Ocelot route must have a key.");
            string serviceName = GetServiceName(routeKey);
            Uri destination = GetDestination(configuration, serviceName);
            string routePath = route.Path;

            destinationOverrides[$"{routePath}:DownstreamScheme"] = destination.Scheme;
            destinationOverrides[$"{routePath}:DownstreamHostAndPorts:0:Host"] = destination.Host;
            destinationOverrides[$"{routePath}:DownstreamHostAndPorts:0:Port"] = destination.Port.ToString(CultureInfo.InvariantCulture);
        }

        return new ConfigurationBuilder()
            .AddConfiguration(routeConfiguration)
            .AddInMemoryCollection(destinationOverrides)
            .Build();
    }

    private static string GetServiceName(string routeKey)
    {
        if (routeKey.StartsWith("catalog-", StringComparison.Ordinal))
        {
            return "Catalog";
        }

        if (routeKey.StartsWith("cart-", StringComparison.Ordinal))
        {
            return "Cart";
        }

        throw new InvalidOperationException($"Ocelot route '{routeKey}' has no configured downstream service.");
    }

    private static Uri GetDestination(IConfiguration configuration, string serviceName)
    {
        string configurationKey = $"Downstream:{serviceName}:BaseUrl";
        string? value = configuration[configurationKey];
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? destination)
            || (destination.Scheme != Uri.UriSchemeHttp && destination.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(destination.UserInfo)
            || !string.IsNullOrEmpty(destination.Query)
            || !string.IsNullOrEmpty(destination.Fragment)
            || destination.AbsolutePath != "/")
        {
            throw new InvalidOperationException($"Configuration '{configurationKey}' must be an absolute HTTP(S) URL without credentials, query, fragment, or a non-root path.");
        }

        return destination;
    }
}