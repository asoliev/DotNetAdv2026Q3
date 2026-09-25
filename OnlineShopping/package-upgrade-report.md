# OnlineShopping — Package Upgrade Report

**Date:** 2026-09-25
**Branch:** `module10_api_gateway`
**Scope:** all 12 projects under `OnlineShopping/` (IdentityService, CatalogService, CartService, Shared/ShoppingAuth), `net10.0`

## Result

| Check (`dotnet list package`) | Before | After |
|---|---|---|
| `--outdated` | 11 packages outdated (across 7 projects) | **none** |
| `--deprecated` | `xunit 2.9.3` (Legacy → `xunit.v3`) | **none** |
| `--vulnerable --include-transitive` | none | none |
| Unit tests | 33 / 33 passed | **33 / 33 passed** |

## Package changes

| Package | Before | After | Projects |
|---|---|---|---|
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.0 | **10.0.12** | ShoppingAuth, IdentityService.Api, CatalogService.Api, CartService.Api |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.0 | **10.0.12** | IdentityService.Tests |
| Microsoft.NET.Test.Sdk | 17.14.1 | **18.10.1** | all 3 test projects |
| coverlet.collector | 6.0.4 | **10.0.1** | all 3 test projects |
| xunit | 2.9.3 | **removed** → replaced by `xunit.v3` | all 3 test projects |
| xunit.v3 | — | **4.0.1** (new) | all 3 test projects |
| xunit.runner.visualstudio | 3.1.4 | **4.0.0** | all 3 test projects |
| Swashbuckle.AspNetCore | 6.6.2 | **10.2.3** | IdentityService.Api, CatalogService.Api, CartService.Api |
| Asp.Versioning.Mvc | 8.1.0 | **10.2.1** | CatalogService.Api, CartService.Api |
| Asp.Versioning.Mvc.ApiExplorer | 8.1.0 | **10.2.1** | CatalogService.Api, CartService.Api |
| RabbitMQ.Client | 6.8.1 | **7.2.2** | CatalogService.Api, CartService.Api |

Unchanged (already latest): `LiteDB 5.0.21`, `Microsoft.Data.Sqlite 10.0.12`.

---

## Step 1 — ASP.NET Core patch updates (10.0.0 → 10.0.12)

- **Changes:** version bumps only, in 5 `.csproj` files.
- **Code changes:** none.
- **Verified:** build, and 33/33 tests pass.

## Step 2 — Test tooling and the xunit → xunit.v3 migration

**Package changes:** Test.Sdk 18.10.1, coverlet 10.0.1, `xunit` → `xunit.v3` 4.0.1, runner 4.0.0.

**Test project changes** (`*.Tests.csproj`, all 3):

```xml
<OutputType>Exe</OutputType>
<IsTestingPlatformApplication>false</IsTestingPlatformApplication>
<NoWarn>$(NoWarn);CA2007</NoWarn>   <!-- Cart & Identity; Catalog already had it -->
```

- `OutputType=Exe`: xunit.v3 test projects must be executables.
- `IsTestingPlatformApplication=false`:
  - xunit.v3 4.x only ships Microsoft.Testing.Platform (MTP) packages, and they mark the project as an MTP app.
  - The .NET 10 SDK refuses to run MTP apps through the VSTest `dotnet test` path. The error is *"Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK"*.
  - `UseMicrosoftTestingPlatformRunner=false` does **not** fix this. Overriding `IsTestingPlatformApplication` does.
  - This keeps VSTest, `coverlet.collector` and `sonar/scan.sh` (`--collect "XPlat Code Coverage"`) working without changes.
- `NoWarn CA2007`: this matches the existing CatalogService.Tests setting. xUnit's analyzers advise against `ConfigureAwait(false)` in tests.

**Test code changes:**

- **xUnit1051**, a new analyzer in v3 with 28 hits: `TestContext.Current.CancellationToken` is now passed to async calls. I applied xunit's own code fix with `dotnet format analyzers --diagnostics xUnit1051`. Files changed:
  - `CartManagerTests.cs`
  - `LiteDbCartRepositoryTests.cs`
  - `DomainAndApplicationTests.cs`
  - `InfrastructureIntegrationTests.cs`
  - `StartupSmokeTests.cs`
- **Style fixes:** the pre-push hook runs `dotnet format --verify-no-changes` on changed files. Touching these files exposed style issues that were already there, and they would have blocked the push. I fixed them:
  - `CartManagerTests.cs`: `bool removed` → `var removed` (IDE0007).
  - `StartupSmokeTests.cs`:
    - `GetAsync(string)` → `GetAsync(new Uri(..., UriKind.Relative))` (CA2234)
    - `string payload` → `var` (IDE0007)
    - `Assert.Contains(..., StringComparison.Ordinal)` (CA1307)

**Verified:**
- 33/33 tests pass.
- `coverage.cobertura.xml` is still produced for all 3 solutions (Sonar input).
- The pre-push format check passes.

## Step 3 — Swashbuckle 10 + Asp.Versioning 10

**Code changes:**

| File | Change | Reason |
|---|---|---|
| `IdentityService.Api/Program.cs`, `CatalogService.Api/Program.cs`, `CartService.Api/Program.cs` | `using Microsoft.OpenApi.Models;` → `using Microsoft.OpenApi;` | Microsoft.OpenApi v2 (pulled in by Swashbuckle 10) moved its types into the root namespace |
| `IdentityService.Api/Program.cs` | `AddSecurityRequirement(new OpenApiSecurityRequirement { { new OpenApiSecurityScheme { Reference = new OpenApiReference {...} }, [] } })` → `AddSecurityRequirement(document => new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] })` | OpenAPI v2 removed `OpenApiReference`, and Swashbuckle 10 takes a document factory |
| `CatalogService.Api/Program.cs`, `CartService.Api/Program.cs` | removed `AssumeDefaultVersionWhenUnspecified = true`; added `ApiVersionReader = new UrlSegmentApiVersionReader()` | New Asp.Versioning 10 analyzers AV0016/AV0015. Every route is URL-segment versioned (`api/v{version:apiVersion}/...`), so assuming a default version never applied, and the query-string reader was never used. |
| `CartService.Api/Program.cs` | `foreach (string groupName` → `foreach (var groupName` | IDE0007, which already failed before this change and now appears in the pre-push format check |

**Verified:**
- 33/33 tests pass.
- I compared `swagger.json` before and after for all 4 documents (Identity v1, Catalog v1, Cart v1/v2). The only differences are:
  - `"openapi": "3.0.1"` → `"3.0.4"` (spec patch version);
  - a new top-level `tags` list.
- Paths, schemas and the Bearer security scheme/requirement are identical. Swagger UI returns HTTP 200 for all services.
- The end-to-end smoke test (see below) gives the same results on the original and upgraded code, including versioned routing and the `api-supported-versions` headers.

## Step 4 — RabbitMQ.Client 6 → 7 (async API)

The design is unchanged: same topology, same exchanges, queues, dead-letter retry setup and TTL, a connection per publish in Catalog, and one long-lived consumer channel in Cart. Only the client API changed.

| v6 | v7 |
|---|---|
| `CreateConnection()` / `CreateModel()` → `IModel` | `CreateConnectionAsync()` / `CreateChannelAsync()` → `IChannel` (`await using` + `ConfigureAwait(false)`) |
| `ExchangeDeclare`, `QueueDeclare`, `QueueBind` | `ExchangeDeclareAsync`, `QueueDeclareAsync`, `QueueBindAsync` (arguments are now `Dictionary<string, object?>`) |
| `channel.CreateBasicProperties()` + setters | `new BasicProperties { Persistent = true, ContentType = "application/json" }` |
| `BasicPublish(...)` | `BasicPublishAsync(..., mandatory: false, ...)` |
| `DispatchConsumersAsync = true` | removed (async dispatch is always on in v7) |
| `consumer.Received +=` | `consumer.ReceivedAsync +=` |
| `BasicAck` / `BasicNack` / `BasicConsume` | `BasicAckAsync` / `BasicNackAsync` / `BasicConsumeAsync` |

**Files changed:**
- `CatalogService.Api/Messaging/RabbitMqProductEventPublisher.cs`:
  - `PublishAsync` is now truly async; before, it was synchronous and returned `Task.CompletedTask`.
  - Added a targeted `[SuppressMessage("CA1812")]`. The class is created by DI, and the warning already existed but now appears in the pre-push format check.
- `CartService.Api/Messaging/RabbitMqCatalogEventConsumer.cs`: `DeclareTopology` → `DeclareTopologyAsync`, and the handler and ack/nack calls are now async.

**Verified:**
- 33/33 tests pass, and the pre-push format check passes.
- End-to-end smoke test with local `dotnet run` plus RabbitMQ: 14/14 checks pass.
- **Failure path:** I published a malformed message to the exchange. Cart logged *"Failed to process product change message"*, nacked it, the message went to the retry queue, came back after the 5-second TTL and failed again. The service stayed healthy.
- **docker-compose:** `docker compose build` succeeds for all 3 images, and the smoke test against the full compose stack passes 14/14.

---

## End-to-end smoke test (used in steps 3 and 4)

It starts Identity, Catalog and Cart against RabbitMQ and checks:

1. The Identity token is issued and `GET /api/auth/verify` returns 200.
2. An anonymous Catalog write returns 401. The manager creates a category and a product.
3. Catalog v1 returns the `api-supported-versions: 1.0` header, and an unsupported version (`/api/v9`) returns 404, the same as before.
4. The customer adds an item to a cart. The v1 response is an object and the v2 response is an array. The response has `api-supported-versions: 1.0, 2.0`, and `/api/v3` returns 404.
5. **Product update in Catalog → RabbitMQ → the cart item's name and price are updated in Cart.**
6. **Product delete in Catalog → RabbitMQ → the item is removed from the cart.**

## Notes / follow-ups (not changed)

- **Local .NET SDK is 10.0.101.** The Docker images use the floating `sdk:10.0` / `aspnet:10.0` tags and already get the latest patch. Consider updating the local SDK to match the 10.0.12 runtime.
- **`rabbitmq:3-management` in `docker-compose.yml`.** RabbitMQ 4.x is available, and 3.x is getting close to end of support. The v7 client supports RabbitMQ 4, but I didn't change this because it's an infrastructure change worth testing separately.
- **Microsoft.Testing.Platform:** the test projects are explicitly opted out (`IsTestingPlatformApplication=false`). To move to MTP later:
  - add `global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`;
  - replace `coverlet.collector` with an MTP coverage extension;
  - update `sonar/scan.sh`.
- **Existing behavior, not introduced by this upgrade:** a message that always fails (for example, malformed JSON) cycles forever between the main queue and the retry queue, because there's no retry limit. Consider checking the `x-death` count and parking the message after N attempts.
- **Existing behavior:** if RabbitMQ is down when Cart starts, the consumer's `ExecuteAsync` throws and the host stops. The same thing happened on v6.
- **Existing analyzer warnings** in files I didn't touch (CA1707 test names, CA1062, CA1305, CA1861, CA1002/CA2227) were left alone.

## Files changed

```
Shared/ShoppingAuth/ShoppingAuth.csproj
IdentityService/src/IdentityService.Api/IdentityService.Api.csproj
IdentityService/src/IdentityService.Api/Program.cs
IdentityService/tests/IdentityService.Tests/IdentityService.Tests.csproj
IdentityService/tests/IdentityService.Tests/StartupSmokeTests.cs
CatalogService/src/CatalogService.Api/CatalogService.Api.csproj
CatalogService/src/CatalogService.Api/Program.cs
CatalogService/src/CatalogService.Api/Messaging/RabbitMqProductEventPublisher.cs
CatalogService/tests/CatalogService.Tests/CatalogService.Tests.csproj
CatalogService/tests/CatalogService.Tests/DomainAndApplicationTests.cs
CatalogService/tests/CatalogService.Tests/InfrastructureIntegrationTests.cs
CartService/src/CartService.Api/CartService.Api.csproj
CartService/src/CartService.Api/Program.cs
CartService/src/CartService.Api/Messaging/RabbitMqCatalogEventConsumer.cs
CartService/tests/CartService.Tests/CartService.Tests.csproj
CartService/tests/CartService.Tests/CartManagerTests.cs
CartService/tests/CartService.Tests/LiteDbCartRepositoryTests.cs
```

17 files, +145 / −117 lines. Nothing has been committed.
