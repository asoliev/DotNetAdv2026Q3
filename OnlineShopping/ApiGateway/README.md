# API Gateway

The .NET 10 Ocelot gateway is the client entry point for Catalog and Cart. The development Compose stack publishes it at `http://localhost:5004`. Identity remains a separate token issuer at `http://localhost:5003` and is not proxied by the gateway.

## Run

Start the full development stack from the repository root:

```sh
docker compose -f OnlineShopping/docker-compose.yml up --build -d
```

The Compose host ports are Gateway `5004`, Cart `5001`, Catalog `5002`, and Identity `5003`. Compose configures the gateway's downstream destinations with Docker service DNS:

- `Downstream__Catalog__BaseUrl=http://catalog-service:8080`
- `Downstream__Cart__BaseUrl=http://cart-service:8080`

To run only the supporting services in Compose and run the gateway as a local process, use the local `appsettings.json` destinations (`http://localhost:5002` for Catalog and `http://localhost:5001` for Cart):

```sh
docker compose -f OnlineShopping/docker-compose.yml up --build -d rabbitmq identity-service catalog-service cart-service
ASPNETCORE_URLS=http://localhost:5004 dotnet run --no-launch-profile --project OnlineShopping/ApiGateway/src/ApiGateway.Api/ApiGateway.Api.csproj
```

Stop the stack with `docker compose -f OnlineShopping/docker-compose.yml down`. Do not add `-v` unless intentionally deleting the named Catalog and Cart data volumes.

The project pins Ocelot `25.0.1`, Ocelot.Cache.CacheManager `25.0.0`, Swashbuckle.AspNetCore `10.2.3`, and Microsoft.AspNetCore.Authentication.JwtBearer `10.0.12` in [ApiGateway.Api.csproj](src/ApiGateway.Api/ApiGateway.Api.csproj).

The test project pins Microsoft.AspNetCore.Mvc.Testing `10.0.12`, coverlet.collector `10.0.1`, Microsoft.NET.Test.Sdk `18.10.1`, xunit.runner.visualstudio `4.0.0`, and xunit.v3 `4.0.1`.

## Routes And Access

Upstream and downstream API paths are the same.

| Method and path | Access |
| --- | --- |
| `GET /api/v1/categories`, `GET /api/v1/categories/{id}` | Anonymous |
| `GET /api/v1/categories/{categoryId}/products` | Anonymous; paginated |
| `GET /api/v1/products`, `GET /api/v1/products/{id}` | Anonymous |
| `GET /api/v1/products/{id}/properties`, `GET /api/v1/products/{id}/aggregate` | Anonymous |
| Catalog `POST`, `PUT`, and `DELETE` routes for categories/products | Bearer token with role `admin` at both gateway and Catalog |
| Cart `GET /api/v1/carts/{cartKey}`, `POST /api/v1/carts/{cartKey}/items`, `DELETE /api/v1/carts/{cartKey}/items/{itemId}` | Bearer token; Cart accepts `Manager` or `Store customer` |
| The corresponding Cart `/api/v2/...` routes | Same access as v1 |

Catalog product pages accept `categoryId`, `pageNumber` (default `1`), and `pageSize` (default `20`). Category-specific product pages use the same page query parameters. Catalog `PATCH` paths are protected at the gateway but are not implemented by Catalog; do not use or advertise them as supported operations. A Manager-only token is not an admin token and is denied Catalog writes. The admin account does not have either Cart role and cannot use Cart endpoints.

`GET /api/v1/products/{id}/properties` returns the fixed demo values `category=Samsung` and `model=s10` for an existing product; these are not persisted product specifications. The aggregate route combines the unchanged product response and properties as `{ "product": ..., "properties": ... }`. A missing product returns 404, downstream/invalid-response failures return 502, and a downstream timeout returns 504. Its two downstream requests have a five-second route timeout.

## Swagger

Open the gateway UI at `http://localhost:5004/swagger`. It exposes these root-relative documents:

- `http://localhost:5004/swagger/catalog/v1/swagger.json`
- `http://localhost:5004/swagger/cart/v1/swagger.json`
- `http://localhost:5004/swagger/cart/v2/swagger.json`

The gateway fetches documents only for those configured services/versions, removes upstream server URLs, and presents the gateway root as the server. Catalog reads and the aggregate operation are anonymous; Catalog writes require `admin`; Cart operations require Bearer and document the existing `Manager`/`Store customer` role alternatives. Unknown document selections return 404; upstream fetch or document failures return 502 without exposing internal error details.

## Caching

The gateway uses an in-memory cache with a 60-second TTL for only these anonymous Catalog list routes:

- `GET /api/v1/categories`
- `GET /api/v1/categories/{categoryId}/products`
- `GET /api/v1/products`

Cached lists can remain stale for up to 60 seconds after a Catalog write. Detail, properties, aggregate, write, and Cart routes are not cached. Cache entries are process-local and are lost when the gateway process restarts.

## Smoke Workflow For T16

This workflow is a handoff, not a claim that it has been run. Start the stack and wait for Identity, Catalog, Cart, and Gateway to be ready before issuing requests. It requires `curl` and `jq`. The sample creates unique Catalog records and a unique cart; use the returned IDs rather than seeded data or `Location` headers.

The Catalog create `Location` transform currently uses `GlobalConfiguration.BaseUrl` from `ocelot.json` (`http://localhost:5000`), while Compose publishes the gateway on port `5004`. The response-body IDs below avoid relying on that mismatch. T16 should verify and resolve the public `Location` authority before treating it as a usable Compose URL.

Run from the repository root in one shell. Demo credentials are listed in the [Identity Service README](../IdentityService/README.md); these commands keep issued tokens in shell variables and do not print them.

```sh
set -eu
IDENTITY_URL=http://localhost:5003
GATEWAY_URL=http://localhost:5004
RUN_ID=$(date +%s)

get_token() {
	jq -n --arg userName "$1" --arg password "$2" '{userName:$userName,password:$password}' |
		curl -fsS -H 'Content-Type: application/json' --data-binary @- "$IDENTITY_URL/api/auth/token" |
		jq -r '.accessToken'
}

ADMIN_TOKEN=$(get_token 'admin@shop.local' 'Admin123!')
CUSTOMER_TOKEN=$(get_token 'customer@shop.local' 'Customer123!')

CATEGORY_BODY=$(jq -n --arg name "gateway-docs-$RUN_ID" '{name:$name,image:null,parentCategoryId:null}')
CATEGORY=$(printf '%s' "$CATEGORY_BODY" |
	curl -fsS -H "Authorization: Bearer $ADMIN_TOKEN" -H 'Content-Type: application/json' \
		--data-binary @- "$GATEWAY_URL/api/v1/categories")
CATEGORY_ID=$(printf '%s' "$CATEGORY" | jq -r '.id')

PRODUCT_BODY=$(jq -n --arg name "gateway-docs-product-$RUN_ID" --arg categoryId "$CATEGORY_ID" \
	'{name:$name,description:"Gateway smoke item",image:null,categoryId:$categoryId,price:1.25,amount:1}')
PRODUCT=$(printf '%s' "$PRODUCT_BODY" |
	curl -fsS -H "Authorization: Bearer $ADMIN_TOKEN" -H 'Content-Type: application/json' \
		--data-binary @- "$GATEWAY_URL/api/v1/products")
PRODUCT_ID=$(printf '%s' "$PRODUCT" | jq -r '.id')

# These reads are anonymous.
curl -fsS "$GATEWAY_URL/api/v1/products/$PRODUCT_ID/properties"
curl -fsS "$GATEWAY_URL/api/v1/products/$PRODUCT_ID/aggregate"
curl -fsS "$GATEWAY_URL/api/v1/products?categoryId=$CATEGORY_ID&pageNumber=1&pageSize=20"
curl -fsS "$GATEWAY_URL/api/v1/categories/$CATEGORY_ID/products?pageNumber=1&pageSize=20"

CART_KEY="gateway-docs-$RUN_ID"
PRODUCT_NAME=$(printf '%s' "$PRODUCT" | jq -r '.name')
PRODUCT_PRICE=$(printf '%s' "$PRODUCT" | jq -r '.price')
CART_ITEM=$(jq -n --arg id "$PRODUCT_ID" --arg name "$PRODUCT_NAME" --argjson price "$PRODUCT_PRICE" \
	'{id:$id,name:$name,image:null,price:$price,quantity:1}')
CART=$(printf '%s' "$CART_ITEM" |
	curl -fsS -H "Authorization: Bearer $CUSTOMER_TOKEN" -H 'Content-Type: application/json' \
		--data-binary @- "$GATEWAY_URL/api/v1/carts/$CART_KEY/items")
ITEM_ID=$(printf '%s' "$CART" | jq -r '.items[0].id')
curl -fsS -H "Authorization: Bearer $CUSTOMER_TOKEN" "$GATEWAY_URL/api/v1/carts/$CART_KEY"
curl -fsS -X DELETE -H "Authorization: Bearer $CUSTOMER_TOKEN" \
	"$GATEWAY_URL/api/v1/carts/$CART_KEY/items/$ITEM_ID"

# A Manager token must receive 403 for Catalog writes; it is not an admin substitute.
MANAGER_TOKEN=$(get_token 'manager@shop.local' 'Manager123!')
curl -i -H "Authorization: Bearer $MANAGER_TOKEN" -H 'Content-Type: application/json' \
	--data-binary "$CATEGORY_BODY" "$GATEWAY_URL/api/v1/categories"
```

For the cache acceptance check, use a separate unique list query: prime it, mutate matching Catalog data as admin, verify it remains cached before expiry, then verify it refreshes after the 60-second TTL. This smoke workflow does not measure cache expiry.

The example removes its Cart item but leaves its generated category and product in the named Catalog volume. The timestamped names keep repeated runs distinct; remove those records as admin after acceptance if they are no longer needed.

## Development Limitations

The seeded accounts, shared signing configuration, plaintext HTTP ports, and demo Catalog properties are for local development only. The signing configuration is not production-safe; use production secret management, TLS, network restrictions, and operational controls before deployment. The Identity service keeps refresh-token state in memory.