# OnlineShopping — Package Upgrade Report

**Date:** 2026-09-25
**Branch:** `update_packages` (commit `3a0a403`)
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
- `coverage.opencover.xml` is still produced for all 3 solutions (Sonar input).
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

## Full endpoint verification (after the upgrade commit `3a0a403`)

The smoke test above covered only part of the API. I also ran a full-coverage script that calls **every endpoint** of all 3 services, including negative, auth and validation cases. It asserts the status code or value for **110 checks**.

I ran it three times:
- on the **original code** (commit `dbd5f92`, in a separate git worktree);
- on the **upgraded code** with local `dotnet run` and RabbitMQ;
- against the **docker-compose stack** built from the upgraded code.

| Service | Endpoints called | Cases covered |
|---|---|---|
| Identity | `POST /api/auth/token`, `POST /api/auth/refresh`, `GET /api/auth/verify` | manager/customer login, roles and permissions, wrong password or unknown user → 401, missing body → 400, verify with valid/anonymous/garbage/tampered-signature/tampered-payload tokens, refresh rotation, reuse of a revoked refresh token → 401, invalid refresh token → 401, refreshed access token accepted by Identity and Catalog |
| Catalog – categories | `GET`, `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}` | anonymous → 401, customer → 403 (on POST, PUT and DELETE), create with image, child category with parent, unknown parent → 400, missing body → 400, `Location` header, GET missing/non-guid → 404, update and read back, delete then delete again → 404, `api-supported-versions`, unsupported version → 404 |
| Catalog – products | `GET {id}`, `GET ?categoryId&pageNumber&pageSize`, `GET categories/{id}/products`, `POST`, `PUT {id}`, `DELETE {id}` | 401/403, create with image, unknown category → 400, missing body → 400, paging (counts, page 2 of size 2), category filter, unknown category → 404, route-based category listing and paging, update with unknown category → 400, delete missing → 404, **category delete cascades to its products** |
| Cart | `GET v1`, `GET v2`, `POST items` (v1 and v2), `DELETE items/{id}` (v1 and v2) | anonymous or garbage token → 401, manager and customer both allowed, empty cart, add with image, merge on re-add (quantity 2+3=5), missing body → 400, v1 object vs v2 array shape, `api-supported-versions: 1.0, 2.0`, v3 → 404, delete → 200, delete again → 404, non-guid → 404 |
| RabbitMQ | Catalog → Cart | product update changes the cart item's name, price and image and **keeps the quantity**; product delete removes the item from the cart; queues are empty afterwards |
| Swagger | 4 `swagger.json` documents + 3 Swagger UIs | every UI asset (`swagger-ui-bundle.js`, `index.css`, …) returns 200, and Cart UI lists V1 and V2 |

**Results**

| Run | Result |
|---|---|
| Original code (local) | 110 / 110 |
| **Upgraded code (local)** | **110 / 110** |
| **Upgraded code (docker-compose)** | 109 / 110. The only failure is the unfiltered product count: 5 instead of 4, because the existing "Laptop Pro" product in the compose volume is included. This is not a regression. |

**Response parity:** I diffed the normalized responses (GUIDs, JWTs, timestamps and trace IDs masked) between the original and upgraded runs. **97 of 110 lines are identical.** The 13 differences are expected:
- a random GUID in the `Location` check;
- the source path inside the Development stack traces of the 500 responses (listed below);
- `openapi 3.0.1 → 3.0.4` in the 4 swagger documents;
- the newer Swagger UI HTML bundle.

**Existing behavior, the same before and after:** domain validation errors are not handled in the controllers and return **500** instead of 400:
- empty category name;
- negative product price;
- `pageSize=101` / `pageNumber=0`;
- cart quantity 0;
- relative cart image URL.

The test records these for parity only; this upgrade doesn't change them. A fix would be to catch `ArgumentException` in the controllers, or to add a ProblemDetails exception handler.

I cleaned up all test data. The existing "Electronics" / "Laptop Pro" data in the compose volumes was left untouched.

## What is still not on the latest version

**Direct NuGet packages:** all are at their latest. `--vulnerable --include-transitive` and `--deprecated` report none in every solution.

**Transitive packages** (brought in by other packages; left on purpose):

| Package | Resolved | Latest | Why not pinned |
|---|---|---|---|
| Microsoft.OpenApi | 2.7.5 | 3.10.2 | Swashbuckle 10 is built against OpenApi 2.x, and 3.x is a breaking major version |
| SQLitePCLRaw.* | 2.1.12 | 3.x | comes from `Microsoft.Data.Sqlite 10.0.12` (latest); pinning a new major under it isn't supported |
| Microsoft.IdentityModel.* / System.IdentityModel.Tokens.Jwt | 8.19.2 | 8.23.0 | comes from JwtBearer 10.0.12; minor updates with no known vulnerabilities, so they could be pinned but it isn't needed |
| Asp.Versioning.Http | 10.2.1 | 10.2.3 | comes from Asp.Versioning.Mvc 10.2.1, which is the latest Mvc release |
| Microsoft.Bcl.Cryptography, ApiDescription.Server, Microsoft.Testing.*, ApplicationInsights, Bcl.AsyncInterfaces | older patch or minor | newer | come from the ASP.NET / test SDK packages; not used directly |

## Notes / follow-ups (not changed)

- **Local .NET SDK is 10.0.101, with runtime 10.0.1.** This is 11 runtime patches behind 10.0.12, which the NuGet packages target. It affects only local `dotnet run`/`dotnet test`. The Docker images use the floating `sdk:10.0` / `aspnet:10.0` tags and get the latest patch on every build. Consider installing the latest 10.0 SDK locally.
- **RabbitMQ: done, `rabbitmq:3-management` (3.13.7) → `rabbitmq:4-management` (4.3.6)** in `docker-compose.yml`.
  - **Why no migration was needed:** the broker has no data volume, so every start is a fresh node. The 3.13 → 4.x migration rules (enable all feature flags, go through 4.2) therefore don't apply.
  - **Compatibility:** the topology uses only durable classic queues, dead-lettering and TTL, and all of these work the same on 4.x. It doesn't use classic mirrored queues or transient non-exclusive queues, the two features 4.x removed or deprecated.
  - **Verified:**
    - the full 110-check suite passes locally with responses identical to the 3.13 run;
    - the compose stack gives the same result as the 3.13 run (109/110, with the one expected existing-data difference);
    - the malformed-message nack → retry queue → 5 s TTL → redelivery cycle works.
  - **Broker log:** the one deprecation warning is `management_metrics_collection`. It's internal to the management plugin and doesn't involve our code or configuration.
  - **Tooling note:** the bundled `rabbitmqadmin` CLI is now the rewritten v2 with a new syntax. Use the management HTTP API or the new syntax in any manual scripts.
- **Microsoft.Testing.Platform:** the test projects are explicitly opted out (`IsTestingPlatformApplication=false`). To move to MTP later:
  - add `global.json` → `"test": { "runner": "Microsoft.Testing.Platform" }`;
  - replace `coverlet.collector` with an MTP coverage extension;
  - update `sonar/scan.sh`.
- **Existing behavior, not introduced by this upgrade:** a message that always fails (for example, malformed JSON) cycles forever between the main queue and the retry queue, because there's no retry limit. Consider checking the `x-death` count and parking the message after N attempts.
- **Existing behavior:** if RabbitMQ is down when Cart starts, the consumer's `ExecuteAsync` throws and the host stops. The same thing happened on v6.
- **Existing analyzer warnings** in files I didn't touch (CA1707 test names, CA1062, CA1305, CA1861, CA1002/CA2227) were left alone. *Update:* all of these except CA1002/CA2227 on `CartDocument.Items` were fixed or suppressed later; see `sonarqube-report.md`.

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

17 files, +145 / −117 lines. These are committed in `3a0a403`; the full-verification section of this report is a later, uncommitted edit.
