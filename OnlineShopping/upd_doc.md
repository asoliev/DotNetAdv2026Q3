I checked all 12 projects in OnlineShopping (Identity, Cart, Catalog and Shared/ShoppingAuth, all on net10.0) against nuget.org. None of the packages have known vulnerabilities, but most have newer versions available. I didn't change anything.

Patch updates (same version line, safe to take now)

┌───────────────────────────────────────────────┬──────────────────┬───────────────────────────────────────────────────┐
│                    Package                    │ Current → Latest │                      Used in                      │
├───────────────────────────────────────────────┼──────────────────┼───────────────────────────────────────────────────┤
│ Microsoft.AspNetCore.Authentication.JwtBearer │ 10.0.0 → 10.0.12 │ ShoppingAuth, Identity.Api, Cart.Api, Catalog.Api │
├───────────────────────────────────────────────┼──────────────────┼───────────────────────────────────────────────────┤
│ Microsoft.AspNetCore.Mvc.Testing              │ 10.0.0 → 10.0.12 │ IdentityService.Tests                             │
└───────────────────────────────────────────────┴──────────────────┴───────────────────────────────────────────────────┘

You already use Microsoft.Data.Sqlite 10.0.12, so the ASP.NET packages are the ones behind. Moving them all to 10.0.12 keeps everything on the same servicing version.

Major updates (may need code changes)

┌────────────────────────────────┬───────────────┬──────────────────────────────────┬─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│            Package             │  Current →    │             Used in              │                                                                                               What to expect                                                                                                │
│                                │    Latest     │                                  │                                                                                                                                                                                                             │
├────────────────────────────────┼───────────────┼──────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ RabbitMQ.Client                │ 6.8.1 → 7.2.2 │ Catalog.Api (publisher),         │ Biggest change. v7 is async-only: IModel becomes IChannel, CreateModel() becomes CreateChannelAsync(), BasicPublish becomes BasicPublishAsync, and EventingBasicConsumer becomes                            │
│                                │               │ Cart.Api (consumer)              │ AsyncEventingBasicConsumer. Only 2 files need rewriting: RabbitMqProductEventPublisher.cs and RabbitMqCatalogEventConsumer.cs.                                                                              │
├────────────────────────────────┼───────────────┼──────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Swashbuckle.AspNetCore         │ 6.6.2 →       │ all 3 Api projects               │ 10.x moves to Microsoft.OpenApi v2. Any custom filters or OpenApiSecurityScheme / JWT security setup will need adjusting.                                                                                   │
│                                │ 10.2.3        │                                  │                                                                                                                                                                                                             │
├────────────────────────────────┼───────────────┼──────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Asp.Versioning.Mvc /           │ 8.1.0 →       │ Cart.Api, Catalog.Api            │ Mostly drop-in. Update them together with Swashbuckle, because the API-version Swagger setup uses both.                                                                                                     │
│ .ApiExplorer                   │ 10.2.1        │                                  │                                                                                                                                                                                                             │
├────────────────────────────────┼───────────────┼──────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Microsoft.NET.Test.Sdk         │ 17.14.1 →     │ all 3 test projects              │ Usually drop-in.                                                                                                                                                                                            │
│                                │ 18.10.1       │                                  │                                                                                                                                                                                                             │
├────────────────────────────────┼───────────────┼──────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ coverlet.collector             │ 6.0.4 →       │ all 3 test projects              │ Check that the Sonar coverage collection setup (sonar/) still works.                                                                                                                                        │
│                                │ 10.0.1        │                                  │                                                                                                                                                                                                             │
├────────────────────────────────┼───────────────┼──────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ xunit.runner.visualstudio      │ 3.1.4 → 4.0.0 │ all 3 test projects              │ Pair this with the xunit migration below rather than upgrading it alone.                                                                                                                                    │
└────────────────────────────────┴───────────────┴──────────────────────────────────┴─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘

Deprecated package

- xunit 2.9.3 is flagged as Legacy on nuget.org, which points to xunit.v3 as the replacement. Migrating means:
  - swapping the package reference;
  - making the test projects executables (OutputType Exe);
  - making small API adjustments, such as IAsyncLifetime now returning ValueTask.

Already current

- LiteDB 5.0.21
- Microsoft.Data.Sqlite 10.0.12
- The Business Logic, Data Access, Domain, Application and Infrastructure library projects have nothing to update.

Outside NuGet

- The Dockerfiles use the floating sdk:10.0 and aspnet:10.0 tags, so they get the latest 10.0 patch on each pull.
- The local SDK is 10.0.101. That's worth updating to the newest 10.0.1xx release to match the 10.0.12 runtime.
- docker-compose.yml uses rabbitmq:3-management. RabbitMQ 4.x is available, and 3.x is getting close to end of support.

Suggested order

1. Patch updates (JwtBearer and Mvc.Testing to 10.0.12), then run the tests.
2. Test tooling: Test.Sdk and coverlet, then the xunit.v3 and runner 4.x migration.
3. Swashbuckle 10 together with Asp.Versioning 10, one Api project at a time.
4. RabbitMQ.Client 7 with the async rewrite of the publisher and consumer, then an end-to-end check with docker-compose.

I can start with step 1 if you'd like. It's low risk and touches only .csproj files.