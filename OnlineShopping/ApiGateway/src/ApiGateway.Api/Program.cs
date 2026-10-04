using ApiGateway.Api;

using Ocelot.Cache.CacheManager;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

using ShoppingAuth;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IConfiguration routeConfiguration = GatewayRouteConfiguration.Load(builder.Configuration, builder.Environment.ContentRootPath);

builder.Services.AddShoppingJwtAuthentication();
builder.Services.AddOcelot(routeConfiguration)
	.AddCacheManager(options => options.WithDictionaryHandle());

WebApplication app = builder.Build();

await app.UseOcelot().ConfigureAwait(false);
await app.RunAsync().ConfigureAwait(false);

public partial class Program { }