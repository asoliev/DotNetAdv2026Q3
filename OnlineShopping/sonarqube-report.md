# OnlineShopping — SonarQube Cleanup Report

**Date:** 2026-09-28, follow-up on 2026-10-02
**Branch:** `enhance_project`
**Scope:** all 3 scanned projects (IdentityService, CatalogService, CartService) plus the shared `sonar/scan.sh` tooling
**Status:** all SonarQube findings fixed; only commit and local cleanup remain.
- Sections 1–6 below were committed in `b4a0bdc`.
- The 2026-10-02 follow-up code fixes (S6964, S6966, S1192, S8969, S2077, CA2227/CA1002) were committed in `2daaf96` and `1748fdd`. See [Follow-up — 2026-10-02](#follow-up--2026-10-02).
- The SonarQube 26.9 / Postgres 18 upgrade and the S2068 fix were committed in `78013f0`.
- Not committed yet: the ASP0018 fix and the `scan.sh` coverage fix.
- Sections 1–7 hold the 2026-09-28 numbers. The current numbers are in [Final scan (2026-10-02)](#final-scan-2026-10-02): 0 open issues and all ratings A.

This report replaces the running commentary in `sonar_news.txt` (kept as raw chat history) with a single before/after summary, organized by what was wrong, what was fixed, and what's still open.

---

## Starting point

The first scan looked clean — 0 bugs, 0 vulnerabilities, all ratings A — but all 3 quality gates were failing, and two problems in `sonar/scan.sh` meant the numbers weren't trustworthy:

1. **Coverage always showed near 0%.** The script exported Cobertura, but SonarQube's C# analyzer only reads OpenCover.
2. **The Api projects and `ShoppingAuth` were never scanned.** `dotnet test <sln>` only builds test projects and what they reference; Catalog's and Cart's tests didn't reference their `*.Api` projects, so `CatalogService.Api`, `CartService.Api` and `ShoppingAuth` were invisible to Sonar — no issues, no coverage, nothing.

Once the script was fixed and a corrected rescan was run, the real baseline was:

| | Identity | Catalog | Cart |
|---|---|---|---|
| Quality gate | ❌ | ❌ | ❌ |
| Coverage | 91.1% | 43.1% | 63.1% |
| Code smells | 24 | 39 | 13 |
| Security hotspots to review | 3 | 6 | 4 |
| Bugs / vulnerabilities | 0 / 0 | 0 / 0 | 0 / 0 |

All 3 gates failed for the same root cause — 0% of security hotspots reviewed — and Catalog/Cart additionally failed on new-code coverage.

---

## What was fixed

### 1. `sonar/scan.sh` — make the report trustworthy (done, uncommitted)

- Coverage format switched from Cobertura to OpenCover: `sonar.cs.cobertura.reportsPaths` → `sonar.cs.opencover.reportsPaths`, and `dotnet test --collect "XPlat Code Coverage"` → `dotnet build --no-incremental` followed by `dotnet test --no-build --collect "XPlat Code Coverage;Format=opencover"`. The explicit rebuild forces every project to be analyzed, not just the ones the tests reference.
- `sonar/README.md` updated to describe the new coverage pipeline (and later, the RabbitMQ exclusion — see below).
- Verified with a real rescan of all 3 projects: Api projects and `ShoppingAuth` are now analyzed and their coverage counted.

### 2. Security hotspots — containers running as root (done, uncommitted)

- All 3 Dockerfiles now run as the base image's non-root `app` user: added `USER $APP_UID`. Catalog and Cart also `chown` their `/app/data` directory to that user so the app can still write its LiteDB/SQLite files.
- Existing Docker volumes (`onlineshopping_catalog-data`, `onlineshopping_cart-data`) were owned by root; ownership was changed to the app user with a one-time `chown`. No data was touched — verified "Electronics"/"Laptop Pro" survived.
- Verified: full compose rebuild, all 3 containers run as `app`, the 110-endpoint suite still passes (109/110, same pre-existing volume-data difference as before).
- Result: all 3 Dockerfile hotspots are gone from the scan.

### 3. Security hotspots — remaining 4 (blocked, not done)

These are false positives that still need to be marked **Safe** in the SonarQube UI (or via API — an automated hotspot-status call was blocked by tool permissions both times it was attempted):

- Catalog `SqliteProductRepository.cs:54` and `:67` — SQL injection: the inserted text is one of a fixed set of strings, every value is a bound parameter.
- `appsettings.json` in Catalog and Cart — hard-coded `guest/guest` RabbitMQ password: local-dev only, overridden by environment variables in docker-compose.

**This is the one item blocking the Cart and Catalog gates from going green** once coverage and code smells are addressed — see "Next steps."

> **2026-10-02:** the two SQL lines were rewritten as fixed, fully parameterized queries, so those 2 hotspots should go away on the next scan. Only the 2 `guest/guest` hotspots will still need review.

### 4. Coverage — Catalog and Cart Api-level tests (done, uncommitted)

Added the same kind of API-level integration tests Identity already had, so the previously-invisible `*.Api` projects are now exercised:

- **New test infrastructure:** `CartApiFactory.cs`, `CatalogApiFactory.cs` (in-memory `WebApplicationFactory` setups), `TestTokens.cs` for both, `RecordingProductEventPublisher.cs` (Catalog).
- **`CatalogApiTests.cs`** (238 lines): authorization on every write endpoint, category/product create/read/update/delete, validation errors, paging, category-delete cascading to its products.
- **`CartApiTests.cs`** (143 lines): anonymous/garbage-token 401s, manager/customer access, empty cart, merge-on-re-add, v1/v2 shape differences, delete/redelete.
- **`RabbitMqCatalogEventConsumerTests.cs`** (91 lines): to make Cart's message handling testable without a live broker, its handler logic was pulled out into its own method (behavior unchanged); tests cover product-update propagating name/price/image while keeping quantity, product-delete removing the item, and malformed messages being rejected.
- **`DomainValidationTests.cs`** (Catalog, 107 lines): domain-level validation checks.
- Identity's `.slnx` never listed its own test project (the old `.sln` did), so its tests were never run by the scanner at all — fixed by adding the `/tests/` folder entry to `IdentityService.slnx`.

Coverage after these tests:

| Service | Overall coverage | New-code coverage (gate needs 80%) |
|---|---|---|
| Identity | 91.1% | 100% ✅ |
| Cart | 91.9% | 89.6% ✅ (after RabbitMQ exclusion, below) |
| Catalog | 98.0% | 90.5% ✅ (after RabbitMQ exclusion, below) |

### 5. Coverage — RabbitMQ connection code (done, uncommitted — chose option A)

Catalog's `RabbitMqProductEventPublisher` and the connection/queue setup in Cart's consumer only run against a real broker, so they stayed at 0% even after the new tests. Two options were on the table — exclude the files from coverage (quick), or add Testcontainers-based integration tests against a real broker (genuine coverage, slower/heavier). **Option A (exclude) was applied:**

```
/d:sonar.coverage.exclusions="**/Messaging/RabbitMq*.cs"
```

added to `scan.sh`. The files are still analyzed for bugs/smells/hotspots, they just don't count toward the coverage metric. `sonar/README.md` documents the exclusion and the reasoning. Option B (real-broker tests) remains a possible later improvement, not done.

### 6. Code smells (32 of 54 fixed, uncommitted)

- **S4457 / CA1062 (×12, argument checks split out of async methods):** `CategoryService`, `ProductService`, `SqliteCategoryRepository`, `SqliteProductRepository`, `CategoriesController`, `ProductsController` — each public method now does its `ArgumentNullException.ThrowIfNull` synchronously and delegates to a private async method for the rest (the pattern the codebase already used for `DeleteAsync`). The repositories now reject null where they previously didn't check at all.
- **CA1305:** `Convert.ToInt32(result)` → `Convert.ToInt32(result, CultureInfo.InvariantCulture)` in `SqliteProductRepository.cs`.
- **S4136:** the two `MapImage` overloads in `ProductsController.cs` moved next to each other.
- **S3267:** the Swagger endpoint loop in `CatalogService.Api/Program.cs` rewritten with `.Select(...)`.
- **AV0011:** removed the redundant `options.DefaultApiVersion = new ApiVersion(1, 0)` line from both Catalog's and Cart's `Program.cs` — 1.0 is already the default.
- **ASP0027 / S1118:** removed the now-unnecessary `public partial class Program { }` from `IdentityService.Api/Program.cs` (not needed on .NET 10).
- **IDE0028 (×7) / CA1861 (×5):** test arrays switched to collection expressions (`[]`), e.g. `Cart.cs`'s and `CartDocument.cs`'s `new()` → `[]`.
- **xUnit2033:** `Assert.Single(...).Name` pattern applied in a Catalog test.
- **CA1707 (test method underscores), 22 issues:** suppressed at the project level instead of touching every test name — `<NoWarn>$(NoWarn);CA2007;CA1707</NoWarn>` added to all 3 `*.Tests.csproj`, consistent with the existing `CA2007` suppression. Production code still gets CA1707 checked.

All 3 solutions' tests pass after these changes (Cart 31, Catalog 31, Identity 14 at the time of this pass — later grew further with the API tests above), and every touched file passes `dotnet format`.

### 7. Code smells — 3 items needing a decision (open, not done)

1. **S3604 (×15) / S3928 (×4):** false positives from SonarQube 9.9's analyzer (v8.51), which predates C# 12 primary constructors — it misreads fields like `_database = new CatalogDatabase(databasePath)` as "overwritten by a constructor" when there is no other constructor. Confirmed as a genuine analyzer bug: **all 19 disappeared under SonarQube 26.9** with no code changes (see below). No fix needed if/when 26.9 is adopted; otherwise they'd need to be marked false-positive in the 9.9 UI.
2. **ASP0018** in `ProductsController.cs:46`: likely a false positive — `{version:apiVersion}` in the route is consumed by the API-versioning library, not by the action's parameters. Still present in 26.9. Options: mark false-positive, or add an unused `ApiVersion version` parameter. **Fixed 2026-10-02**, see [ASP0018 fix](#asp0018-fix-2026-10-02).
3. **CA2227 / CA1002** on `CartDocument.Items`: this is the LiteDB storage model, and LiteDB needs a public settable `List<T>` to deserialize into. Removing the setter risks breaking existing carts. Still present in 26.9. Options: mark won't-fix in SonarQube, or add a targeted `[SuppressMessage]` with justification. **2026-10-02:** a code fix that LiteDB accepts was found, `IList<T>` with an `init` setter. See the follow-up section.

---

## SonarQube version evaluation (9.9 vs 26.9)

Separately from the code-quality work, a local (non-Docker) SonarQube **26.9 Community Build** was stood up on `:9100` alongside the existing Docker-based **9.9** on `:9000`, to check whether upgrading would remove the analyzer false positives above. Result: **yes**.

| | Cart | Catalog | Identity |
|---|---|---|---|
| Code smells | 2 → 6 | 17 → 7 | 3 → 1 |
| Vulnerabilities | 0 → 1 | 0 → 3 | 0 → 0 |
| Coverage | 91.8% | 98.1% | 91.1% |

- **Gone under 26.9:** all 19 S3604/S3928 primary-constructor false positives — confirms they were an analyzer limitation, not a real issue.
- **Still reported under 26.9:** ASP0018 and CA2227/CA1002 (items 2–3 above). Both were fixed on 2026-10-02.
- **New under 26.9** (didn't exist as findings under 9.9's older ruleset):
  - **S6964 ×6** in `Contracts.cs` — non-nullable numeric request fields (`Price`, `Quantity`, etc.) that aren't marked required; an omitted field silently becomes `0` instead of failing validation. Flagged as worth fixing.
  - **S6966 ×3** — `app.Run()` should be `await app.RunAsync()`.
  - **S1192** — the literal `"$categoryId"` repeated 4 times; should be a constant.
  - **S8969** — an unnecessary `!` in `SqliteProductRepository.cs:159`.
  - **S2077 ×2** (now surfaced as a vulnerability, not just a hotspot) — same two SQL lines from item 3 in the security-hotspots section. Re-verified as a false positive (fixed strings + parameterized values); could be rewritten as two separate fixed queries to make the analyzer stop flagging it permanently.
  - **S2068 ×2** (now a vulnerability) — the same `guest/guest` password hotspot, carried forward.

**Caveat noted at the time:** 26.9's gates all showed OK, but that's not meaningful yet — a project's first analysis has no "new code" baseline, so gate conditions aren't evaluated until the second scan.

These fixes were applied on 2026-10-02. See [Follow-up — 2026-10-02](#follow-up--2026-10-02).

---

## Current state of the two SonarQube instances

- **Docker SonarQube 9.9** (`sonarqube:lts-community` + `postgres:16`) — running on `:9000`, unchanged, still the "official" instance referenced by `scan.sh`.
- **Local SonarQube 26.9** — running directly (not in Docker) on `:9100`, login still `admin/admin` (not changed), with a temporary token `claude-temp-scan-new` still active. Left running for inspection. Standing it up required: creating the `sonarqube` DB/role in Postgres 18.6 by hand (nothing existed), overriding a broken JDBC URL in `sonar.properties` for that session only (a stray `#` targets a nonexistent database/schema — file itself is unchanged), using Java 21 (what 26.9's bundled runtime targets, not 25), and disabling the macOS-blocking Elasticsearch bootstrap checks.

Neither `sonar.properties` nor `scan.sh`'s target host was changed.

**Update 2026-10-02:** the Docker stack was upgraded to 26.9 on `:9000`. The local 26.9 instance on `:9100` was cleaned up and rescanned. See "Style fixes and local instance rescan" below.

---

## Net result as of 2026-09-28 (SonarQube 9.9)

| Service | Code smells before → now | Coverage | Gate |
|---|---|---|---|
| Identity | 24 → 3 | 91.1% | ✅ OK |
| Cart | 13 → 2 | 91.9% | ❌ (4 unreviewed hotspots only) |
| Catalog | 39 → 17 | 98.0% | ❌ (4 unreviewed hotspots only) |

54 → 22 code smells fixed; the remaining 22 are the 19 old-analyzer false positives (item 7.1) plus the 3 judgment-call items (7.2/7.3). Bugs and vulnerabilities remain 0 under 9.9. All tests pass throughout (76 at last count under 9.9, before the API-level test additions grew that further).

---

---

## Follow-up — 2026-10-02

### Code changes (on disk, not committed)

| Rule | File(s) | Change |
|---|---|---|
| **S6964**: value-type request fields weren't required | Cart and Catalog `Contracts.cs` | `CartItemRequest.Id/Price/Quantity` and `ProductUpsertRequest.CategoryId/Price/Amount` are now `[property: JsonRequired]`. A request without one of them now gets **400** from model binding instead of being stored with `0` / `Guid.Empty`. **This tightens the API contract.** |
| **S2077**: SQL built from strings (also the 2 SQL hotspots) | `SqliteProductRepository.cs` | `GetPageAsync` no longer interpolates a `WHERE` clause. Two `const` queries use `WHERE ($categoryId IS NULL OR CategoryId = $categoryId)`, and `$categoryId` is always bound (`DBNull` when there's no filter). |
| **S1192**: `"$categoryId"` repeated 4 times | `SqliteProductRepository.cs` | Replaced with a `CategoryIdParameter` constant. |
| **S8969**: unnecessary `!` | `SqliteProductRepository.cs` | Removed from `product.Description!`. |
| **S6966**: `app.Run()` | all 3 `Program.cs` | Now `await app.RunAsync().ConfigureAwait(false)`. |
| **CA2227 / CA1002**: settable `List<T>` property | Cart `CartDocument.cs` | `Items` is now `IList<CartItemDocument> { get; init; }`. LiteDB still has a setter to fill it on load, and the stored BSON shape is unchanged. |

**New tests:** `CartApiTests.ItemWithMissingRequiredFieldIsRejected` and `CatalogApiTests.ProductWithMissingRequiredFieldIsRejected`. Each has 3 cases, one per required field left out, and expects 400.

**Verified locally:** 82/82 tests pass (Identity 14, Cart 34, Catalog 34). All 3 solutions build with 0 analyzer warnings.

### Local coverage (Coverlet OpenCover, 2026-10-02)

There has been no SonarQube rescan yet. These numbers come from the same `coverage.opencover.xml` that `scan.sh` uploads, with the `**/Messaging/RabbitMq*.cs` exclusion applied. SonarQube calculates coverage from lines and conditions together and skips generated code, so its numbers will differ slightly.

| Service | Line coverage | Branch coverage | Weakest spots |
|---|---|---|---|
| Identity | 91.8% (190/207) | 87.5% (35/40) | `ShoppingAuth` is at 39.3%: `ShoppingJwtAuthenticationExtensions` isn't exercised by Identity's tests. It is covered by Cart's and Catalog's tests, but SonarQube scores each project separately. |
| Cart | 93.0% (401/431) | 81.9% (77/94) | Guard clauses in `CartItem`, the error path in `AccessTokenLoggingMiddleware`, generated `LoggerMessage` code |
| Catalog | 99.4% (635/639) | 91.4% (117/128) | `CatalogDatabase` constructor guard |

### Analyzer warnings left in the build (2026-10-02)

- **No build warnings are left.** The last two, CA2227 / CA1002 on `CartDocument.Items`, were fixed. The options were tested against the LiteDB round-trip tests:
  - `IList<CartItemDocument> Items { get; init; } = [];` clears both warnings, and all 34 Cart tests pass. **Applied.**
  - `IList<CartItemDocument> Items { get; private set; } = [];` also clears both warnings, and all tests pass.
  - A getter-only `Items { get; }` **breaks persistence**: LiteDB skips properties without a setter, so carts reload empty and 4 tests fail. This is why the setter has to stay.
  - `[SuppressMessage]` was not needed.
- **IDE style warnings shown only by `dotnet format`**, in files not touched by this work:
  - IDE0007 / IDE0008 (`var` vs. explicit type) in `IdentityServiceTests.cs`, `AccessTokenLoggingMiddleware.cs` and `LiteDbCartRepository.cs`.
  - IDE0290 (use a primary constructor) in `CartsController.cs`, `AccessTokenLoggingMiddleware.cs` and `CartService.cs`.
  - **Fixed 2026-10-02.** The `var` rules don't contradict each other. `.editorconfig` asks for `var` with built-in and apparent types and an explicit type everywhere else, and each file broke one half of that.
  - **Why the build never showed them:** `.editorconfig` sets their severity with the `option = value:warning` suffix. `dotnet format` honors that suffix, but the compiler (`EnforceCodeStyleInBuild`) only honors `dotnet_diagnostic.IDExxxx.severity` lines.
  - Still open: about 35 info-level suggestions. These are IDE0090 (`new()`), IDE0300/IDE0301/IDE0305 (collection expressions) and IDE0017 (object initializer).

## SonarQube 26.9 scan — 2026-10-02

This is the first analysis on the new Docker stack: Community Build `26.9.0.129388` on `postgres:18.6`, scanner 11.3.0. All 3 projects were scanned with `sonar/scan.sh`.

Because this is the first analysis of each project, the quality gate's "new code" conditions were not evaluated yet. They will be from the next scan onward.

| Metric | Cart | Catalog | Identity |
|---|---|---|---|
| Quality gate | Passed | Passed | Passed |
| Coverage | 92.3% | 97.6% | 91.1% |
| Line coverage | 94.0% | 98.6% | 91.8% |
| Branch coverage | 84.1% | 92.8% | 87.5% |
| Uncovered lines (of lines to cover) | 25 of 416 | 9 of 663 | 17 of 207 |
| Bugs | 0 | 0 | 0 |
| Vulnerabilities | 1 | 1 | 0 |
| Code smells | 0 | 1 | 0 |
| Security hotspots | 0 | 0 | 0 |
| Duplication | 0% | 0% | 0% |
| Lines of code | 780 | 1034 | 362 |
| Cognitive complexity | 51 | 55 | 15 |
| Technical debt | 0 min | 0 min | 0 min |
| Reliability / Security / Maintainability | A / **C** / A | A / **C** / A | A / A / A |

**Remaining issues (3):**

| Rule | Where | Severity | Note |
|---|---|---|---|
| `csharpsquid:S2068` hard-coded password | `CartService/src/CartService.Api/appsettings.json:5` | Vulnerability | RabbitMQ `guest/guest` dev login. This is the only reason Security is rated C. |
| `csharpsquid:S2068` hard-coded password | `CatalogService/src/CatalogService.Api/appsettings.json:5` | Vulnerability | Same as above. |
| `external_roslyn:ASP0018` unused route parameter `version` | `CatalogService/src/CatalogService.Api/ProductsController.cs:46` | Info | The route parameter is used by API versioning, not by the action. |

**Gone compared with earlier scans:**

- S6964, S6966, S1192, S8969 and S2077;
- the 2 SQL-injection hotspots;
- CA2227 / CA1002;
- all the 9.9-era false positives.

### S2068 fix and rescan (2026-10-02)

The RabbitMQ login was moved out of the repository:

- Cart's and Catalog's `appsettings.json` now only contain `RabbitMq:Host`.
- `RabbitMqCatalogEventConsumer` and `RabbitMqProductEventPublisher` no longer hard-code a `"guest"` fallback. They only set `UserName` / `Password` when configuration provides them. Otherwise the RabbitMQ client's built-in default is used, which is the same `guest` login, accepted only from `localhost`, so local `dotnet run` behaves as before.
- Both API projects have a `UserSecretsId` (`online-shopping-cartservice-api`, `online-shopping-catalogservice-api`), so `dotnet user-secrets set "RabbitMq:Password" ...` works locally.
- `docker-compose.yml` already passed the credentials as `RabbitMq__Username` / `RabbitMq__Password` environment variables. Only its comments changed. See the root `README.md` → *RabbitMQ credentials*.

On the rescan, the second analysis evaluated the gate's new-code conditions, and both passed:

| | Cart | Catalog |
|---|---|---|
| Quality gate | Passed | Passed |
| Vulnerabilities | 0 | 0 |
| Security rating | **A** | **A** |
| Coverage | 92.3% | 98.3% |
| Code smells | 0 | 1 (ASP0018) |

Tests: Cart 34/34 and Catalog 34/34 pass, with 0 build warnings.

### ASP0018 fix (2026-10-02)

`GET api/v{version}/categories/{categoryId}/products` was an action on `ProductsController` with a `~/`-rooted template. That template contained `{version}`, and no action parameter bound it, so ASP0018 fired. The parameter is actually consumed by API versioning; class-level `[Route]` templates aren't checked by ASP0018.

- The endpoint moved into a new `CategoryProductsController` with a class-level `[Route("api/v{version:apiVersion}/categories/{categoryId:guid}/products")]`.
  - The URL, query parameters, paging, response and 404 for an unknown category are unchanged.
  - The existing `ProductsArePagedAndFilteredByCategory` test still covers it.
  - In Swagger the endpoint is now listed under *CategoryProducts* instead of *Products*.
- `ProductResponseMapper` (internal, static) now holds the `Product` → `ProductResponse` / `PageResponse` mapping, which both controllers use. `ProductsController` no longer needs `GetPageInternal`.
- There are no suppressions.

### `scan.sh` coverage fix (2026-10-02)

The ASP0018 rescan first failed with:

```
IllegalStateException: Line 134 is out of range in the file CatalogService/src/CatalogService.Api/ProductsController.cs (lines: 131)
```

**Cause:** `scan.sh` imported every `tests/**/TestResults/**/coverage.opencover.xml`, including reports left over from earlier `dotnet test` runs. Old reports described line numbers of older source files, so:

- a shrunk file made the import fail;
- otherwise they were merged in silently, which skewed coverage. For example, Catalog moved between 97.6% and 98.3% with no test changes.

**Fix:** each scan now writes coverage to a fresh `mktemp -d` folder (`dotnet test --results-directory`), imports only that, and deletes it on exit. Existing `TestResults` folders are left alone; they are git-ignored.

### Final scan (2026-10-02)

The scan used the fixed `scan.sh`, with exactly 1 coverage report per project.

| Metric | Cart | Catalog | Identity |
|---|---|---|---|
| Quality gate (incl. new-code conditions) | Passed | Passed (new-code coverage 100%) | Passed |
| Coverage | 91.9% | 98.1% | 91.1% |
| Line / branch coverage | 93.9% / 83.0% | 99.4% / 91.5% | 91.8% / 87.5% |
| Uncovered lines (of lines to cover) | 25 of 407 | 4 of 648 | 17 of 207 |
| Bugs / vulnerabilities / code smells / hotspots | 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 | 0 / 0 / 0 / 0 |
| Duplication / technical debt | 0% / 0 min | 0% / 0 min | 0% / 0 min |
| Lines of code | 783 | 1064 | 362 |
| Reliability / Security / Maintainability | A / A / A | A / A / A | A / A / A |

**Open issues: 0 in all 3 projects.**

These coverage numbers are slightly below the earlier 26.9 scans. Those scans had stale reports merged in, so the numbers above are the accurate ones.

### Style fixes and local instance rescan (2026-10-02)

- **Docker rescan after the IDE0007/IDE0008/IDE0290 fixes:** Cart and Identity still have 0 issues and pass the gate. Cart coverage is 91.8%, down from 91.9%, because the primary-constructor null checks (`?? throw`) are branches no test reaches. Cart new-code coverage is 88.9% (the gate needs at least 80%).
- **Local 26.9 instance (`:9100`) cleaned up:**
  - The admin password was reset.
  - The `claude-temp-scan-new` token was revoked and replaced by `local-scan`.
  - `sonar.properties` and `sonar.sh` were fixed (JDBC URL; the custom macOS block was removed).
- **`scan.sh` now reads `SONAR_HOST_URL`**, defaulting to `http://localhost:9000`.
- **Local instance rescanned with the current code.** Its 18 issues from 2026-09-28 are gone, and the results match Docker:

| | Cart | Catalog | Identity |
|---|---|---|---|
| Quality gate | Passed | Passed | Passed |
| Open issues | 0 | 0 | 0 |
| Reliability / Security / Maintainability | A / A / A | A / A / A | A / A / A |
| Coverage | 91.8% | 98.1% | 91.1% |
| Lines of code | 770 | 1064 | 362 |

### Info-level style fixes (2026-10-02)

`dotnet format style --severity info` applied all 35 suggestions in 19 files:

- 14 × IDE0090: target-typed `new(...)`.
- 20 × IDE0300/0301/0305: collection expressions, for example `[.. items.Select(Map)]` and `?? []`.
- 1 × IDE0017: an object initializer in a test.

The formatter also sorted 2 `using` blocks. No behavior changed.

Afterwards the build has 0 warnings, the tests pass (Cart 34/34, Catalog 34/34, Identity 14/14), and `dotnet format style --severity info --verify-no-changes` reports nothing left. Rescanned on both servers:

| | Cart | Catalog | Identity |
|---|---|---|---|
| Quality gate (Docker and local) | Passed | Passed | Passed |
| Open issues | 0 | 0 | 0 |
| Coverage | 91.8% | 98.1% | 91.1% |
| New-code coverage (Docker / local) | 95.6% / 96.3% | 100% / 100% | 100% / 100% |

## Next steps (decisions needed from you)

1. ~~**S2068, the RabbitMQ `guest/guest` login**~~: done (2026-10-02), see above.
2. ~~**ASP0018**~~: done (2026-10-02), `CategoryProductsController`.
3. ~~**`CartDocument.Items`**~~: done (2026-10-02).
4. ~~**SonarQube version**~~: done (2026-10-02). The Docker stack runs Community Build `26.9.0.129388` (Temurin 25 inside the image) on `postgres:18.6`, and all 3 projects have been scanned.
   - The old 9.9 volumes are kept for rollback; see `sonar/README.md` to remove them.
5. ~~**Clean up the standalone local 26.9 instance**~~: done (2026-10-02), rescanned with 0 issues.
6. ~~**IDE0007/IDE0008/IDE0290 style warnings**~~: done (2026-10-02). Old `TestResults` folders were deleted. All 35 info-level suggestions are fixed as well.
7. **Commit** the ASP0018 fix, the `scan.sh` fix and these docs. The SonarQube upgrade and S2068 were committed in `78013f0`.
