# API Gateway

Thin .NET 10 Ocelot gateway for the OnlineShopping services. The initial scaffold configures one anonymous Catalog route, `GET /api/v1/categories`; runtime forwarding is not yet verified and is covered by T05.

## Configuration

Set `Downstream:Catalog:BaseUrl` and `Downstream:Cart:BaseUrl` to absolute HTTP(S) base URLs with no credentials, query, fragment, or non-root path. Route keys beginning with `catalog-` use Catalog; keys beginning with `cart-` use Cart. Unknown route-key prefixes fail startup. Route JSON is loaded with `IConfiguration`, and only the named destination scheme, host, and port are overlaid.

## Package Pins

- Ocelot `25.0.1` requested and resolved; its package includes a `net10.0` target.
- Ocelot.Cache.CacheManager `25.0.0` requested and resolved; its `net10.0` nuspec group requires Ocelot `25.0.0` or newer, CacheManager packages `3.0.0`, and Microsoft.Extensions configuration/DI/logging packages `10.0.10`. Restore resolves Ocelot `25.0.1`, CacheManager packages `3.0.0`, and Microsoft.Extensions packages `10.0.12`.
- Swashbuckle.AspNetCore `10.2.3` requested and resolved.
- API test dependencies match the existing Catalog test project and all resolve as pinned: Microsoft.AspNetCore.Mvc.Testing `10.0.12`, coverlet.collector `10.0.1`, Microsoft.NET.Test.Sdk `18.10.1`, xunit.runner.visualstudio `4.0.0`, and xunit.v3 `4.0.1`.

## Verified Ocelot APIs

- The installed Ocelot 25.0.1 API exposes `AddOcelot(IServiceCollection, IConfiguration)` and `IConfigurationBuilder.AddOcelot(FileConfiguration, ...)`; the scaffold uses a structured `IConfiguration` root with destination overlays and builds using `AddOcelot(configuration)` plus `await app.UseOcelot()`.
- Installed XML docs specify static route `Timeout` in seconds and `QoSOptions.Timeout` in milliseconds. For the planned five-second route bound, use `Timeout: 5` rather than a QoS option.
- Ocelot 25's `IDefinedAggregator.Aggregate(List<HttpContext>)` returns `Task<DownstreamResponse>`. The aggregate context exposes each response through `context.Items.DownstreamResponse()`.
- The installed Ocelot assembly contains `IDefinedAggregator` and `AddSingletonDefinedAggregator`; Ocelot 25 documents `AddSingletonDefinedAggregator<T>()` as the Ocelot builder registration API. Aggregator implementation/registration is reserved for its later task.