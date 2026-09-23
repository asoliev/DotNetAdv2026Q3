# Identity Service

This service issues and verifies JWT access tokens for the OnlineShopping APIs.

Seeded demo users:

- `manager@shop.local` / `Manager123!`
- `customer@shop.local` / `Customer123!`

Endpoints:

- `POST /api/auth/token` - issue access and refresh tokens
- `POST /api/auth/refresh` - rotate a refresh token
- `GET /api/auth/verify` - verify the current bearer access token