using System.Globalization;
using System.Text.Json.Nodes;

using ApiGateway.Api;

using Ocelot.Cache.CacheManager;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

using ShoppingAuth;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IConfiguration routeConfiguration = GatewayRouteConfiguration.Load(builder.Configuration, builder.Environment.ContentRootPath);
builder.Configuration.AddConfiguration(routeConfiguration);
string? aggregateTimeoutSetting = builder.Configuration["Gateway:AggregateTimeoutSeconds"];
if (aggregateTimeoutSetting is not null)
{
	if (!int.TryParse(aggregateTimeoutSetting, NumberStyles.None, CultureInfo.InvariantCulture, out int timeoutSeconds)
		|| timeoutSeconds is < 1 or > 5)
	{
		throw new InvalidOperationException("Gateway:AggregateTimeoutSeconds must be an integer between 1 and 5 seconds.");
	}

	var timeoutOverrides = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
	foreach (IConfigurationSection route in routeConfiguration.GetSection("Routes").GetChildren())
	{
		if (route["Key"] is "catalog-product-get" or "catalog-product-properties")
		{
			timeoutOverrides[$"{route.Path}:Timeout"] = timeoutSeconds.ToString(CultureInfo.InvariantCulture);
		}
	}

	routeConfiguration = new ConfigurationBuilder()
		.AddConfiguration(routeConfiguration)
		.AddInMemoryCollection(timeoutOverrides)
		.Build();
}

builder.Services.AddShoppingJwtAuthentication();
builder.Services.AddHttpClient<GatewaySwaggerDocumentProvider>();

var ocelotBuilder = builder.Services.AddOcelot(routeConfiguration)
	.AddCacheManager(options => options.WithDictionaryHandle());
ocelotBuilder.AddSingletonDefinedAggregator<ProductDetailsAggregator>();

WebApplication app = builder.Build();

MapSwaggerEndpoints(app);

await app.UseOcelot().ConfigureAwait(false);
await app.RunAsync().ConfigureAwait(false);

static void MapSwaggerEndpoints(WebApplication application)
{
	application.Use(async (context, next) =>
	{
		string[] segments = (context.Request.Path.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
		if (!HttpMethods.IsGet(context.Request.Method)
			|| segments.Length != 4
			|| !segments[0].Equals("swagger", StringComparison.OrdinalIgnoreCase)
			|| !segments[3].Equals("swagger.json", StringComparison.OrdinalIgnoreCase))
		{
			await next(context).ConfigureAwait(false);
			return;
		}

		GatewaySwaggerDocumentProvider provider = context.RequestServices.GetRequiredService<GatewaySwaggerDocumentProvider>();
		try
		{
			JsonObject document = await provider.GetDocumentAsync(segments[1], segments[2], context.RequestAborted).ConfigureAwait(false);
			context.Response.ContentType = "application/json; charset=utf-8";
			await context.Response.WriteAsync(document.ToJsonString(), context.RequestAborted).ConfigureAwait(false);
		}
		catch (GatewaySwaggerDocumentNotFoundException)
		{
			context.Response.StatusCode = StatusCodes.Status404NotFound;
			await context.Response.WriteAsJsonAsync(new { error = "The requested Swagger document is not available." }, context.RequestAborted).ConfigureAwait(false);
		}
		catch (GatewaySwaggerDocumentException)
		{
			context.Response.StatusCode = StatusCodes.Status502BadGateway;
			await context.Response.WriteAsJsonAsync(new { error = "The upstream Swagger document could not be retrieved or parsed." }, context.RequestAborted).ConfigureAwait(false);
		}
	});

	application.UseSwaggerUI(options =>
		{
			options.RoutePrefix = "swagger";
			options.SwaggerEndpoint("/swagger/catalog/v1/swagger.json", "Catalog v1");
			options.SwaggerEndpoint("/swagger/cart/v1/swagger.json", "Cart v1");
			options.SwaggerEndpoint("/swagger/cart/v2/swagger.json", "Cart v2");
			options.EnableSwaggerDocumentUrlsEndpoint();
		});
}

public partial class Program { }