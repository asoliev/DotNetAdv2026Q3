# Task 2 Summary

This document summarizes the work done during Task 2 for the OnlineShopping solution set.

## What Was Done

Task 2 focused on static analysis hardening with SonarQube and the code changes needed to make the services pass the scan cleanly.

The work covered three parts:

- Set up a local SonarQube environment under [sonar/](sonar/).
- Created classic `.sln` files for `CartService`, `CatalogService`, and `IdentityService` so `dotnet-sonarscanner` could analyze the projects.
- Refactored service code to remove the issues reported by SonarQube.

## What Changed

### SonarQube tooling

- Added a local SonarQube stack with PostgreSQL in [sonar/docker-compose.yml](sonar/docker-compose.yml).
- Added a repo-local .NET tool manifest in [sonar/dotnet-tools.json](sonar/dotnet-tools.json).
- Added a reusable scan wrapper in [sonar/scan.sh](sonar/scan.sh).
- Documented the scan workflow in [sonar/README.md](sonar/README.md).

### CartService changes

- Added a classic solution file for SonarScanner compatibility.
- Renamed the business service class to avoid the namespace/type collision.
- Changed cart image URLs from `string` to `Uri` across the domain, persistence, API contracts, and tests.
- Split async controller and middleware methods into validation wrappers plus async core methods where SonarQube flagged mixed validation/work.
- Updated RabbitMQ consumer code to use configuration more safely and removed the hard-coded password issue.
- Updated tests to match the refactored contracts and naming rules.

### CatalogService changes

- Added a classic solution file for SonarScanner compatibility.
- Verified the service with SonarQube after the scan workflow was in place.

### IdentityService changes

- Added a classic solution file for SonarScanner compatibility.
- Made the controller, request/response records, and service classes public so they are valid framework-facing types.
- Added explicit null checks to public entry points flagged by SonarQube.
- Changed the permissions helper to return a concrete `List<string>` instead of `IReadOnlyList<string>`.

## SonarQube Findings

### CartService

The first scan reported 33 major issues. The main categories were:

- Framework-facing public types flagged as unused or incorrectly scoped.
- Async methods that mixed validation and asynchronous work.
- A hard-coded RabbitMQ password.
- Contract and test shapes that needed to be adjusted to match the refactoring.

After the cleanup, the unresolved `BLOCKER`, `CRITICAL`, and `MAJOR` issue count for `online-shopping-cartservice` dropped to 0.

### CatalogService

The scan completed with no unresolved `BLOCKER`, `CRITICAL`, or `MAJOR` issues.

### IdentityService

The first scan reported 6 major issues. The main findings were:

- `CA1812` on internal controller, model, and service types that are actually used by ASP.NET Core.
- `CA1859` on a permissions helper that returned `IReadOnlyList<string>` instead of `List<string>`.

After the refactor and null-guard updates, the unresolved `BLOCKER`, `CRITICAL`, and `MAJOR` issue count for `online-shopping-identityservice` dropped to 0.

## Validation

- `dotnet build` succeeded for the three service solutions.
- SonarQube was rerun after each cleanup step.
- The final unresolved major-or-higher issue count was 0 for CartService, CatalogService, and IdentityService.
