# Identity Service

This service issues and verifies JWT access tokens for the OnlineShopping APIs.

Seeded demo users:

- `admin@shop.local` / `Admin123!` (development-only; Catalog administrator)
- `manager@shop.local` / `Manager123!`
- `customer@shop.local` / `Customer123!`

The demo admin has only the `admin` role for Catalog administration. It does not grant access to Cart endpoints.

Endpoints:

- `POST /api/auth/token` - issue access and refresh tokens
- `POST /api/auth/refresh` - rotate a refresh token
- `GET /api/auth/verify` - verify the current bearer access token