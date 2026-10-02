# OnlineShopping — SonarQube Cleanup Report

**Date:** 2026-09-28, follow-up on 2026-10-02
**Branch:** `enhance_project`
**Scope:** all 3 scanned projects (IdentityService, CatalogService, CartService) plus the shared `sonar/scan.sh` tooling
**Status:** in progress.
- Sections 1–6 below were committed in `b4a0bdc`.
- The 2026-10-02 follow-up (the new 26.9 findings: S6964, S6966, S1192, S8969, S2077) is on disk and not committed yet. See [Follow-up — 2026-10-02](#follow-up--2026-10-02).
- No rescan has been run since the follow-up changes, so the SonarQube numbers in sections 1–7 are from 2026-09-28.

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
2. **ASP0018** in `ProductsController.cs:46`: likely a false positive — `{version:apiVersion}` in the route is consumed by the API-versioning library, not by the action's parameters. Still present in 26.9. Options: mark false-positive, or add an unused `ApiVersion version` parameter.
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
- **Still reported under 26.9:** ASP0018 and CA2227/CA1002 (items 2–3 above).
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

**As of 2026-10-02 both instances are stopped.** The 26.9 database still holds the `admin/admin` login and the `claude-temp-scan-new` token. Revoke the token and change the password before using that instance again.

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
  - IDE0007 / IDE0008 (`var` vs. explicit type) in `IdentityServiceTests.cs`, `AccessTokenLoggingMiddleware.cs` and `LiteDbCartRepository.cs`. The two rules contradict each other between files, which points to an `.editorconfig` inconsistency.
  - IDE0290 (use a primary constructor) in `CartsController.cs`, `AccessTokenLoggingMiddleware.cs` and `CartService.cs`.

## Next steps (decisions needed from you)

1. **Start SonarQube and rescan** to confirm the follow-up fixes. Expected result: S6964, S6966, S1192, S8969 and S2077 are gone, along with the 2 SQL hotspots.
2. **Mark the 2 remaining hotspots Safe:** the `guest/guest` RabbitMQ login in Cart's and Catalog's `appsettings.json`. Alternatively, move those credentials out of `appsettings.json` into environment variables or user-secrets.
3. ~~**`CartDocument.Items`**~~: done (2026-10-02).
4. **ASP0018** on `ProductsController.cs:46`: mark it false-positive, or add an `ApiVersion version` parameter.
5. **SonarQube version:** stay on 9.9 (mark the 19 S3604/S3928 false positives by hand) or move the Docker stack to the current Community Build with Postgres 18. Moving needs a fresh start or a data migration, because the volumes hold scan history, the admin password and tokens.
6. **Clean up the local 26.9 instance:** revoke the `claude-temp-scan-new` token and change `admin/admin`.
7. **Commit** the 2026-10-02 follow-up.
