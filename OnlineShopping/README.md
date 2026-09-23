# OnlineShopping Guide

This folder contains the three services that work together for the security task:

- `IdentityService` issues and verifies JWT access tokens and manages refresh tokens.
- `CatalogService` exposes catalog data and protects write operations with the `Manager` role.
- `CartService` accepts the same JWT tokens for both roles and logs access-token details in middleware.

## How to run

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
