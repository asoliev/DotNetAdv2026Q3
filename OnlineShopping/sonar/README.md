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