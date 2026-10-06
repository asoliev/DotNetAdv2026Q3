# OnlineShopping Guide

For the distributed observability implementation, local Aspire dashboard setup, and operational runbook, see [the observability solution guide](../11_advanced_logging/05-observability-solution.md).

This folder contains the three services that work together for the security task:

- `IdentityService` issues and verifies JWT access tokens and manages refresh tokens.
- `CatalogService` exposes catalog data and protects write operations with the `Manager` role.
- `CartService` accepts the same JWT tokens for both roles and logs access-token details in middleware.

## How to run

### Aspire AppHost (recommended for local development)

Install the .NET 10 SDK and start Docker Desktop (or another Docker-compatible container runtime), then run from the repository root:

```bash
dotnet run --project OnlineShopping/OnlineShopping.AppHost
```

The AppHost builds and starts IdentityService, CatalogService, CartService, and ApiGateway as local .NET processes. Shared libraries and application/domain/data projects are built as dependencies, not launched separately.

It also starts a RabbitMQ container using the official Aspire hosting integration:

- Catalog and Cart wait for RabbitMQ to be healthy before starting.
- Aspire injects the broker connection string, including its allocated port and generated credentials. No manual RabbitMQ credential setup is needed.
- RabbitMQ uses a named data volume so queues and messages survive AppHost restarts. Its container stops with the AppHost.
- The RabbitMQ management UI is exposed on an allocated port. Open the resource's management endpoint in the Aspire dashboard to inspect exchanges, queues, and messages; use the broker credentials shown in the resource configuration.
- The gateway receives Catalog and Cart endpoint addresses from the AppHost.
- Existing OpenTelemetry instrumentation exports logs, traces, and metrics to the AppHost dashboard automatically.

Open `http://localhost:18888` and use the login link printed in the terminal. The dashboard provides resource status, console logs, and start/stop/restart controls in addition to telemetry.

The APIs retain their local ports: Cart `5001`, Catalog `5002`, Identity `5003`, and Gateway `5004`. Swagger is available at `/swagger` on each API. Stop separately launched APIs and the Compose stack before using the AppHost, because the API ports and dashboard ports (`18888`, `4317`, and `18890`) must be free. AppHost and standalone Compose are alternative launch modes, not intended to run together.

RabbitMQ uses dynamically allocated ports, so an existing Homebrew broker on `5672` does not need to be stopped. The AppHost manages its own separate broker and does not manage the Homebrew service. HTTP dashboard transport is enabled only for this local development profile; do not expose these ports publicly.

### Health checks

All four APIs expose anonymous, status-only health endpoints:

- `GET /health/live`: process liveness, independent of external dependencies.
- `GET /health/ready`: readiness, returning HTTP `200` (`Healthy`) or `503` (`Unhealthy`).

Catalog readiness checks a SQLite category lookup and a RabbitMQ connection. Cart readiness checks a LiteDB cart lookup and a RabbitMQ connection. Database probes are small, non-destructive reads through the same repositories used by the APIs; they verify read access, not write permissions or database integrity. RabbitMQ probes verify broker connectivity, not consumer progress or message delivery.

Identity has no external database and uses the basic self check. Gateway readiness calls Catalog and Cart's `/health/ready` endpoints with bounded HTTP timeouts. Dependency checks are registered with a five-second timeout and return no exception details. Synchronous database work cannot be forcibly interrupted by cancellation, so the probes deliberately use single-key lookups.

The AppHost polls `/health/ready` for every API. Gateway startup waits for healthy Catalog and Cart resources, not merely running processes. During an outage, affected resources become unhealthy while `/health/live` remains healthy; health checks alone do not automatically restart services.

### Start services individually

1. Start RabbitMQ locally, because the catalog and cart services exchange product events through it.
2. Start the identity service.
3. Start the catalog service.
4. Start the cart service.

Example from the repository root:

```bash
dotnet run --project OnlineShopping/IdentityService/src/IdentityService.Api/IdentityService.Api.csproj
dotnet run --project OnlineShopping/CatalogService/src/CatalogService.Api/CatalogService.Api.csproj
dotnet run --project OnlineShopping/CartService/src/CartService.Api/CartService.Api.csproj
```

### RabbitMQ credentials

`appsettings.json` only contains the RabbitMQ host. The login is not stored in the repository:

- If no credentials are configured, Catalog and Cart use the client's built-in `guest` login. RabbitMQ only accepts it from `localhost`, so a local broker with default settings works without any setup.
- `docker-compose.yml` passes the login as `RabbitMq__Username` / `RabbitMq__Password` environment variables.
- The AppHost supplies `ConnectionStrings__rabbitmq`, which takes precedence over individual `RabbitMq` settings.
- For a broker with a different login, set the same environment variables, or use user-secrets (both API projects have a `UserSecretsId`):

```bash
dotnet user-secrets set "RabbitMq:Username" "<user>" --project OnlineShopping/CatalogService/src/CatalogService.Api
dotnet user-secrets set "RabbitMq:Password" "<password>" --project OnlineShopping/CatalogService/src/CatalogService.Api
# repeat for OnlineShopping/CartService/src/CartService.Api
```

## Authentication flow

Use the identity service to get an access token and refresh token.

- `POST /api/auth/token` with one of the seeded users.
- Send the returned access token as `Authorization: Bearer <token>` to Catalog and Cart.
- `POST /api/auth/refresh` to rotate the refresh token and obtain a new access token.
- `GET /api/auth/verify` to validate the current bearer token.

Seeded demo users:

- `manager@shop.local` / `Manager123!`
- `customer@shop.local` / `Customer123!`

## Authorization rules

- `Manager` can call all Catalog operations.
- `Store customer` can call Cart operations.
- Both services validate the same issuer, audience, signing key, and role claims.

## Open endpoints

These endpoints do not require authentication:

- `POST /api/auth/token`
- `POST /api/auth/refresh`
- Catalog `GET` endpoints for categories and products

These endpoints require a bearer token:

- `GET /api/auth/verify`
- Catalog `POST`, `PUT`, and `DELETE` endpoints
- All Cart endpoints

## Messages

RabbitMQ messages are not called directly from HTTP clients.

- When you create, update, or delete a product, the Catalog service publishes the corresponding event automatically.
- The Cart service consumes those events automatically while it is running.
- If you want to inspect the broker traffic, use RabbitMQ tooling or the service logs.

## Service notes

- Catalog uses `CatalogService.Api` and exposes Swagger.
- Cart uses `CartService.Api` and exposes Swagger.
- Identity uses `IdentityService.Api` and exposes Swagger.
- Cart also logs parsed access-token details through custom middleware before controller execution.

## Pre-push validation

To make the optional `dotnet format` hook active for this repository, run this once from the repository root:

```bash
git config core.hooksPath .githooks
chmod +x .githooks/pre-push
```

The pre-push hook runs `dotnet format --verify-no-changes` for the changed C# files and then runs `dotnet build` for the affected `OnlineShopping/*.slnx` solutions before a push is allowed.
