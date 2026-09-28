# SonarQube

This folder contains a local SonarQube stack for the OnlineShopping solutions.

## Start SonarQube

From this folder run:

```bash
docker compose up -d
```

Then open <http://localhost:9000> and log in with `admin` / `admin`.

## Scanner setup

The repository uses a local .NET tool manifest for `dotnet-sonarscanner`.

Install it once from this folder:

```bash
dotnet tool restore
```

The scan helper is [scan.sh](scan.sh).

## Analyze a solution

Run a scan from the repository root after SonarQube is running:

```bash
export SONAR_TOKEN=<your-token>
./OnlineShopping/sonar/scan.sh online-shopping-cartservice OnlineShopping/CartService/CartService.sln
```

Repeat the same pattern for `CatalogService` and `IdentityService` if you want separate SonarQube projects.

When a solution has a `tests/` folder, the helper rebuilds the whole solution (so every project, including ones the tests don't reference, is analyzed), runs the tests with Coverlet in OpenCover format, and imports `tests/**/TestResults/**/coverage.opencover.xml`. SonarC# does not read Cobertura reports. The RabbitMQ adapters (`**/Messaging/RabbitMq*.cs`) are excluded from coverage because they only run against a live broker; they are still analyzed for issues. Coverage is a SonarQube metric that can fail the quality gate; it is not counted as a separate issue type.