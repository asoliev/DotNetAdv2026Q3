# Tasks 1 & 2 — What Changed and Why

## External dependencies identified

Before writing any Dockerfile, the first step was to read the source code and identify what each service connects to over the network.

| Dependency | Used by | Kind |
|---|---|---|
| **RabbitMQ** | CatalogService (publisher), CartService (consumer) | Network-external — needs its own container |
| **SQLite** | CatalogService (`catalog.db`) | Embedded/file-based — runs inside the app process, no separate container |
| **LiteDB** | CartService (`cart.db`) | Embedded/file-based — runs inside the app process, no separate container |

SQLite and LiteDB are *embedded* databases — the driver is compiled into the app, and the database is just a file on disk. There is no separate server process to containerize. When running in Docker the file simply lives inside the container's filesystem; we persist it with a **Docker volume** so data survives container restarts.

RabbitMQ is a real network service (listens on port 5672). It runs as a separate process, so it needs its own container. We use the official pre-built image `rabbitmq:3-management`.

---

## Change 1 — appsettings.json introduced

### Why appsettings.json instead of raw environment variables?

The original project had zero `appsettings.json` files and used `Environment.GetEnvironmentVariable()` directly. That works, but it is not idiomatic ASP.NET Core.

The framework provides a **layered configuration system** via `IConfiguration`:

```
appsettings.json          ← baseline defaults (committed to source)
  ↓ overridden by
appsettings.Development.json   ← dev-specific values
  ↓ overridden by
Environment variables     ← Docker / CI / production overrides
  ↓ overridden by
Command-line arguments    ← one-off overrides
```

Benefits:
- **One access pattern everywhere** — always `configuration["Section:Key"]`, not a mix of JSON reads and `GetEnvironmentVariable` calls
- **Self-documenting defaults** — reading `appsettings.json` tells you exactly what the service needs to run
- **Typed binding** — you can bind sections to `record`/`class` types for compile-time safety
- **No magic strings** — the key names are consistent; environment variable overrides just use `__` instead of `:`

Two `appsettings.json` files were created:

`CatalogService/src/CatalogService.Api/appsettings.json`  
`CartService/src/CartService.Api/appsettings.json`

```json
{
  "RabbitMq": {
    "Host": "localhost",
    "Username": "guest",
    "Password": "guest"
  }
}
```

These are the local-development defaults. No secrets are hard-coded here — in production the environment variables override them.

---

## Change 2 — RabbitMQ connection uses IConfiguration

### Problem
Both services hardcoded `HostName = "localhost"` in their `ConnectionFactory`. Inside Docker each container is its own network namespace — `localhost` inside the container is the container itself, **not** the RabbitMQ container. Connecting to `localhost:5672` would fail.

Additionally, the connection settings were built in a C# field initializer, which cannot receive injected dependencies. The `IConfiguration` service is registered in the DI container by `WebApplication.CreateBuilder`, so the clean way to consume it is via constructor injection.

### Fix — publisher (`RabbitMqProductEventPublisher.cs`)

```csharp
// Before — field initializer, hardcoded
private readonly ConnectionFactory _connectionFactory = new()
{
    HostName = "localhost",
    UserName = "guest",
    Password = "guest"
};

// After — constructor injection
private readonly ConnectionFactory _connectionFactory;

public RabbitMqProductEventPublisher(IConfiguration configuration)
{
    _connectionFactory = new ConnectionFactory
    {
        HostName = configuration["RabbitMq:Host"]     ?? "localhost",
        UserName = configuration["RabbitMq:Username"] ?? "guest",
        Password = configuration["RabbitMq:Password"] ?? "guest"
    };
}
```

### Fix — consumer (`RabbitMqCatalogEventConsumer.cs`)

The same pattern. The old `GetRabbitMqPassword()` static helper method (which read directly from `Environment.GetEnvironmentVariable`) was deleted — it is no longer needed.

```csharp
// Before — field initializer + static helper
private readonly ConnectionFactory _connectionFactory = new()
{
    HostName = "localhost",
    Password = GetRabbitMqPassword(),   // called Environment.GetEnvironmentVariable
    ...
};

// After — constructor injection
public RabbitMqCatalogEventConsumer(
    CartManager cartManager,
    ILogger<RabbitMqCatalogEventConsumer> logger,
    IConfiguration configuration)
{
    _connectionFactory = new ConnectionFactory
    {
        HostName = configuration["RabbitMq:Host"]     ?? "localhost",
        UserName = configuration["RabbitMq:Username"] ?? "guest",
        Password = configuration["RabbitMq:Password"] ?? "guest",
        DispatchConsumersAsync = true
    };
}
```

---

## Change 3 — Database path uses IConfiguration

### Problem
Both `Program.cs` files built the database path as:
```csharp
Path.Combine(builder.Environment.ContentRootPath, "catalog.db")
```
Inside a container `ContentRootPath` is `/app`. That file lives inside the container's writable layer — when the container is replaced, the database file is gone.

### Fix

```csharp
// After
var databaseDir = builder.Configuration["Database:Directory"]
    ?? builder.Environment.ContentRootPath;
var databasePath = Path.Combine(databaseDir, "catalog.db");
```

`Database:Directory` is not in `appsettings.json` because its natural default (`ContentRootPath`) is a runtime value. In Docker Compose, `Database__Directory=/app/data` is set and a named volume is mounted there.

---

## Change 4 — Dockerfiles (multi-stage builds)

Three Dockerfiles created, one per service:
- `OnlineShopping/CatalogService/Dockerfile`
- `OnlineShopping/CartService/Dockerfile`
- `OnlineShopping/IdentityService/Dockerfile`

The build context for all three is the **`OnlineShopping/` root** — the shared `Shared/ShoppingAuth` library and `Directory.Build.props` live there and are referenced by every service.

### Two-stage pattern

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build    # ~800 MB — compile only
...
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime  # ~220 MB — no SDK, no compiler
COPY --from=build /app/publish .
```

The SDK stage is discarded completely — the final image only contains the runtime and the published app output. Result: ~4× smaller image and a smaller attack surface (no build tools in production).

### Layer-caching trick

```dockerfile
# 1. Copy only .csproj files → restore (cached as long as packages don't change)
COPY Directory.Build.props ./
COPY .../*.csproj ...
RUN dotnet restore ...

# 2. Copy source code → publish (invalidates only when source changes)
COPY Shared/ Shared/
COPY CatalogService/src/ CatalogService/src/
RUN dotnet publish ...
```

`dotnet restore` downloads NuGet packages — expensive. Docker caches each layer. By separating the restore step from the compile step, a change to any `.cs` file does **not** re-download packages; it reuses the cached restore layer and only re-runs `dotnet publish`. First build: slow. Every rebuild after: fast.

---

## Change 5 — docker-compose.yml (Task 2)

`OnlineShopping/docker-compose.yml` orchestrates all four containers:

| Service | Image | Port |
|---|---|---|
| `rabbitmq` | `rabbitmq:3-management` (pre-built) | 5672, 15672 |
| `identity-service` | built from `IdentityService/Dockerfile` | 5003 |
| `catalog-service` | built from `CatalogService/Dockerfile` | 5002 |
| `cart-service` | built from `CartService/Dockerfile` | 5001 |

Key design decisions:

**`condition: service_healthy` on RabbitMQ**  
`depends_on` alone only waits for the container process to start. RabbitMQ needs ~10–30 s to be ready to accept connections. The `healthcheck` command (`rabbitmq-diagnostics ping`) is tested every 10 s; CatalogService and CartService only start once the check passes.

**Named volumes for databases**  
`catalog-data` and `cart-data` are Docker-managed volumes mounted at `/app/data`. They survive `docker compose down` and container replacements. Only `docker compose down -v` removes them.

**`__` env var naming**  
ASP.NET Core maps environment variables to `IConfiguration` using `__` (double underscore) as the section separator. So `RabbitMq__Host=rabbitmq` in the compose file overrides the `RabbitMq:Host` key from `appsettings.json`:

```yaml
environment:
  RabbitMq__Host: rabbitmq       # overrides appsettings.json "RabbitMq:Host"
  RabbitMq__Username: guest
  RabbitMq__Password: guest
  Database__Directory: /app/data
```

---

## Configuration overview

| appsettings.json key | Env var override | Default | Notes |
|---|---|---|---|
| `RabbitMq:Host` | `RabbitMq__Host` | `localhost` | Docker service name `rabbitmq` in compose |
| `RabbitMq:Username` | `RabbitMq__Username` | `guest` | |
| `RabbitMq:Password` | `RabbitMq__Password` | `guest` | Change in production |
| `Database:Directory` | `Database__Directory` | `ContentRootPath` | Set to `/app/data` in Docker |
| — | `ASPNETCORE_ENVIRONMENT` | — | `Development` in compose |

---

## All changed files (this session)

| Status | File | Purpose |
|---|---|---|
| `M` | `09_Containerization/04-self-check.md` | Self-check answers filled in |
| `A` | `09_Containerization/05-changes-explained.md` | This explanation document |
| `A` | `OnlineShopping/.dockerignore` | Excludes `bin/`, `obj/`, `sonar/` from Docker build context |
| `A` | `OnlineShopping/CartService/Dockerfile` | Multi-stage build for CartService |
| `A` | `OnlineShopping/CatalogService/Dockerfile` | Multi-stage build for CatalogService |
| `A` | `OnlineShopping/IdentityService/Dockerfile` | Multi-stage build for IdentityService |
| `A` | `OnlineShopping/docker-compose.yml` | Orchestrates all 4 containers (Task 2) |
| `A` | `OnlineShopping/CartService/src/CartService.Api/appsettings.json` | RabbitMq default values |
| `A` | `OnlineShopping/CatalogService/src/CatalogService.Api/appsettings.json` | RabbitMq default values |
| `M` | `OnlineShopping/CartService/src/CartService.Api/Messaging/RabbitMqCatalogEventConsumer.cs` | Uses `IConfiguration`; removed `GetRabbitMqPassword()` |
| `M` | `OnlineShopping/CatalogService/src/CatalogService.Api/Messaging/RabbitMqProductEventPublisher.cs` | Uses `IConfiguration` |
| `M` | `OnlineShopping/CartService/src/CartService.Api/Program.cs` | DB path from `builder.Configuration` |
| `M` | `OnlineShopping/CatalogService/src/CatalogService.Api/Program.cs` | DB path from `builder.Configuration` |
| `M` | `OnlineShopping/CartService/src/CartService.Api/Properties/launchSettings.json` | Removed redundant `RABBITMQ_PASSWORD` |

---

## How to build, run, and test (Task 2 verification)

> **Prerequisite:** Docker Desktop must be running.  
> All commands run from the `OnlineShopping/` directory.

### 1. Build all images

```bash
docker compose build
```

First run downloads the base images (~800 MB SDK + ~220 MB runtime) and compiles all three services. Subsequent builds reuse cached layers and are much faster.

### 2. Start all containers

```bash
docker compose up -d
```

Startup order (enforced by health checks):
1. `rabbitmq` starts and waits until `rabbitmq-diagnostics ping` passes (~10–30 s)
2. `identity-service` starts immediately (no external deps)
3. `catalog-service` and `cart-service` start only after RabbitMQ is healthy

### 3. Check container status

```bash
docker compose ps
```

All four containers should show `Up` (RabbitMQ shows `Up (healthy)`).

### 4. Open Swagger UIs

| Service | URL |
|---|---|
| IdentityService | http://localhost:5003/swagger |
| CatalogService | http://localhost:5002/swagger |
| CartService | http://localhost:5001/swagger |
| RabbitMQ management | http://localhost:15672 (guest / guest) |

### 5. End-to-end test

1. **Get a token** — `POST http://localhost:5003/auth/login` with valid credentials
2. **Create a category** — `POST http://localhost:5002/api/v1/categories` (use the token as Bearer)
3. **Create a product** — `POST http://localhost:5002/api/v1/products`
4. **Verify RabbitMQ event** — open the RabbitMQ UI at http://localhost:15672, go to **Queues**, check `online-shopping.cart.products` — the message should have been delivered
5. **Add to cart** — `POST http://localhost:5001/api/v1/carts/{userId}/items` — the product name and price should match what was created in step 3

### 6. Check logs

```bash
docker compose logs -f                  # stream all logs
docker compose logs catalog-service     # single service
```

### 7. Tear down

```bash
docker compose down          # stop containers, keep volumes (data preserved)
docker compose down -v       # stop containers AND delete volumes (wipes all data)
```
