# SonarQube

This folder contains a local SonarQube stack for the OnlineShopping solutions.

## Versions

| Component | Version | Notes |
|---|---|---|
| SonarQube | Community Build `26.9.0.129388` (`sonarqube:26.9.0.129388-community`) | pinned; `sonarqube:community` currently points to the same image |
| Java (inside the SonarQube image) | Eclipse Temurin 25.0.4.1 | bundled in the image; no local Java is needed for the server |
| PostgreSQL | `postgres:18.6` | data is mounted at `/var/lib/postgresql` (Postgres 18 keeps `PGDATA` in `/var/lib/postgresql/18/docker`) |
| Scanner | `dotnet-sonarscanner` 11.3.0 | from [dotnet-tools.json](dotnet-tools.json); it downloads the JRE it needs from the server |

### Upgrade from 9.9 LTS / Postgres 16 (2026-10-02)

The stack used to run `sonarqube:lts-community` (9.9) on `postgres:16`. It was moved to the versions above with a **fresh start**:

- The new stack uses new volumes (`sonarqube_pg18_db`, `sonarqube_26_*`).
- The old volumes (`sonar_sonarqube_db`, `sonar_sonarqube_data`, `sonar_sonarqube_extensions`) are left untouched for rollback.
  - Postgres 18 can't open a Postgres 16 data directory.
  - Keeping the 9.9 history would also need a `pg_dump`/restore plus SonarQube's own upgrade path.
  - Only scan history, the admin password and tokens were lost, and the projects are re-created by the next scan.
- **Rollback:** restore the previous `docker-compose.yml` from git and run `docker compose up -d`.
- **Remove the old data** once you no longer need it: `docker volume rm sonar_sonarqube_db sonar_sonarqube_data sonar_sonarqube_extensions`.

## Start SonarQube

From this folder run:

```bash
docker compose up -d
```

Then open <http://localhost:9000> and log in with `admin` / `admin`. SonarQube makes you change the password on the first login.

Create a token under **My Account → Security** (type *Global Analysis Token* or *User Token*).

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
./OnlineShopping/sonar/scan.sh online-shopping-cartservice OnlineShopping/CartService/CartService.slnx
./OnlineShopping/sonar/scan.sh online-shopping-catalogservice OnlineShopping/CatalogService/CatalogService.slnx
./OnlineShopping/sonar/scan.sh online-shopping-identityservice OnlineShopping/IdentityService/IdentityService.slnx
```

When a solution has a `tests/` folder, the helper:

- rebuilds the whole solution, so every project is analyzed, including ones the tests don't reference;
- runs the tests with Coverlet in OpenCover format;
- imports `tests/**/TestResults/**/coverage.opencover.xml`. SonarC# does not read Cobertura reports.

The RabbitMQ adapters (`**/Messaging/RabbitMq*.cs`) are excluded from coverage because they only run against a live broker; they are still analyzed for issues.

Coverage is a SonarQube metric that can fail the quality gate; it is not counted as a separate issue type.

The quality gate only checks "new code" from the **second** analysis of a project. The first scan after this fresh start always shows the gate as passed.
