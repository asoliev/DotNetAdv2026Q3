# Task 2 — Testing Report

## Environment

| Item | Value |
|---|---|
| Docker Compose file | `OnlineShopping/docker-compose.yml` |
| Services under test | `identity-service`, `catalog-service`, `cart-service`, `rabbitmq` |
| Test method | `curl` HTTP requests against running containers |

---

## Test execution steps

### Step 1 — Start Docker Desktop and build images

```bash
open -a Docker
# waited until daemon was ready
docker compose build          # built 3 service images from Dockerfiles
docker compose up -d          # started all 4 containers
```

**Expected:** all 4 containers in `Up` state, RabbitMQ showing `(healthy)`.  
**Actual:** all 4 containers up and healthy.

```
NAME                       STATUS
online-shopping-cart       Up
online-shopping-catalog    Up
online-shopping-identity   Up
online-shopping-rabbitmq   Up (healthy)
```

---

### Step 2 — Smoke test (HTTP 200 on all endpoints)

```bash
curl -o /dev/null -w "%{http_code}" http://localhost:5003/swagger/index.html   # IdentityService
curl -o /dev/null -w "%{http_code}" http://localhost:5002/swagger/index.html   # CatalogService
curl -o /dev/null -w "%{http_code}" http://localhost:5001/swagger/index.html   # CartService
curl -o /dev/null -w "%{http_code}" http://localhost:15672                     # RabbitMQ UI
```

**Expected:** `200` on all four.  
**Actual:** `200` on all four. ✅

---

### Step 3 — API call to CatalogService (first failure found)

```bash
curl -X POST http://localhost:5002/api/v1/categories \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"name":"Electronics",...}'
```

**Expected:** `201 Created` with the new category.  
**Actual:** `404 Not Found`. ❌

#### Root cause investigation

Checked container logs:

```
No action descriptors found. This may indicate an incorrectly configured
application or missing application parts.
```

Checked Swagger spec:

```json
{ "paths": {} }
```

Zero routes were registered. The issue was in the controller visibility.

#### Root cause

ASP.NET Core's `ControllerFeatureProvider.IsController()` explicitly rejects non-`public` classes:

```csharp
// from ASP.NET Core source
if (!typeInfo.IsPublic && !typeInfo.IsNestedPublic)
    return false;   // internal controllers are silently skipped
```

`CategoriesController` and `ProductsController` were both marked `internal`. At runtime the framework scans the assembly for controllers, skips all non-public types, finds nothing, and registers zero routes.

#### Why it was not caught earlier

- Local `dotnet run` (Debug) was also affected, but development was done primarily through unit/integration tests (`WebApplicationFactory<Program>`), which have access to the assembly's internals and bypass this check.
- The CA1812 analyzer warning ("internal class apparently never instantiated") was present in build output but not treated as a blocker.
- The Release publish inside Docker made the failure visible because requests actually hit the live middleware pipeline.

---

### Step 4 — Fix: make the API layer public

The `internal` modifier was a cascade — making controllers `public` caused compile errors in Docker because all connected types also needed to be `public`.

#### Files changed

| File | Change |
|---|---|
| `CatalogService.Api/CategoriesController.cs` | `internal` → `public` |
| `CatalogService.Api/ProductsController.cs` | `internal` → `public` |
| `CatalogService.Api/Contracts.cs` | all 6 record types `internal` → `public` (`CategoryResponse`, `CategoryUpsertRequest`, `ProductResponse`, `ProductUpsertRequest`, `ImageRequest`, `ImageResponse`, `PageResponse<T>`) |
| `CatalogService.Api/Messaging/IProductEventPublisher.cs` | `internal interface` → `public interface` |
| `CatalogService.Api/Messaging/ProductEventMessages.cs` | all 3 message records `internal` → `public` (`ProductImageMessage`, `ProductChangedMessage`, `ProductDeletedMessage`) |

After the fix, `docker compose build catalog-service` succeeded and the startup log no longer contained the "No action descriptors found" warning.

---

### Step 5 — Re-run API tests after fix

#### 5a. Get JWT token from IdentityService

```bash
POST http://localhost:5003/api/auth/token
Body: { "userName": "manager@shop.local", "password": "Manager123!" }
```

**Result:** `200 OK`, `accessToken` returned with roles `["Manager"]` and permissions `["Create","Delete","Read","Update"]`. ✅

#### 5b. Create a category in CatalogService

```bash
POST http://localhost:5002/api/v1/categories
Authorization: Bearer <token>
Body: { "name": "Electronics", "image": null, "parentCategoryId": null }
```

**Result:** `200 OK`
```json
{ "id": "9ebd8ddf-cc75-45db-9b4a-11c4f1201bbc", "name": "Electronics", "image": null, "parentCategoryId": null }
```
✅

#### 5c. Create a product in CatalogService (triggers RabbitMQ event)

```bash
POST http://localhost:5002/api/v1/products
Authorization: Bearer <token>
Body: { "name": "Laptop Pro", "description": "Powerful laptop", "image": null,
        "categoryId": "9ebd8ddf-...", "price": 1299.99, "amount": 10 }
```

**Result:** `200 OK`
```json
{ "id": "78f48637-0cd4-4fe9-b04b-8d45adf91422", "name": "Laptop Pro", "price": 1299.99, ... }
```
✅ Product created. CatalogService also published a `product.changed` event to RabbitMQ.

#### 5d. Verify RabbitMQ delivered the event to CartService

```bash
GET http://localhost:15672/api/queues/%2F/online-shopping.cart.products
Authorization: Basic guest:guest
```

**Result:**
```
Messages ready    : 0
Messages delivered: 1
Messages total    : 0
```
✅ The event was published by CatalogService and consumed by CartService. Queue is empty — no stuck messages.

#### 5e. Add product to cart in CartService

```bash
POST http://localhost:5001/api/v1/carts/user-123/items
Authorization: Bearer <customer token>
Body: { "id": "78f48637-...", "name": "Laptop Pro", "image": null, "price": 1299.99, "quantity": 2 }
```

**Result:** `200 OK`
```json
{
  "cartKey": "user-123",
  "items": [{ "id": "78f48637-...", "name": "Laptop Pro", "price": 1299.99, "quantity": 2 }]
}
```
✅

#### 5f. Read the cart back

```bash
GET http://localhost:5001/api/v1/carts/user-123
Authorization: Bearer <customer token>
```

**Result:** Cart returned with `Laptop Pro × 2 @ $1299.99`. ✅

---

## Summary

| # | Test | Result |
|---|---|---|
| 1 | All 4 containers start and reach healthy state | ✅ Pass |
| 2 | Swagger UI responds with HTTP 200 on all 3 services | ✅ Pass |
| 3 | RabbitMQ management UI responds with HTTP 200 | ✅ Pass |
| 4 | IdentityService issues JWT token | ✅ Pass |
| 5 | CatalogService creates category (initially failed, fixed) | ✅ Pass after fix |
| 6 | CatalogService creates product | ✅ Pass after fix |
| 7 | RabbitMQ queue receives and delivers the product-changed event | ✅ Pass |
| 8 | CartService adds item to cart | ✅ Pass |
| 9 | CartService returns cart with correct item | ✅ Pass |

### Bug found and fixed during testing

**Bug:** `CategoriesController` and `ProductsController` in CatalogService were `internal`, causing zero API routes to be registered at runtime in the Docker container.

**Fix:** Changed the access modifier of both controllers, all 7 contract types in `Contracts.cs`, the `IProductEventPublisher` interface, and all 3 message records in `ProductEventMessages.cs` from `internal` to `public`.

**Root cause note:** ASP.NET Core's `ControllerFeatureProvider` silently ignores non-public controller classes. The bug existed before containerization but was invisible because local development relied on tests that bypass this check. Containerization (running a real Release publish against live HTTP requests) made the failure obvious.
