# Identity Service

Identity issues and verifies JWT access tokens for the OnlineShopping APIs. In the development Compose stack it is available directly at `http://localhost:5003`; the API Gateway does not proxy Identity. Obtain tokens from Identity, then send them to the gateway or the corresponding API.

## Development Accounts

| User name | Password | Role | Intended access |
| --- | --- | --- | --- |
| `admin@shop.local` | `Admin123!` | `admin` | Catalog writes |
| `manager@shop.local` | `Manager123!` | `Manager` | Cart; not Catalog writes |
| `customer@shop.local` | `Customer123!` | `Store customer` | Cart |

These credentials are hard-coded demonstration accounts for local development only. The admin account has only the `admin` role: it can administer Catalog but does not satisfy Cart's `Manager` or `Store customer` policy. Catalog writes require `admin` at both the gateway and Catalog service, so a Manager-only token is denied. Catalog reads are anonymous.

## Endpoints

| Method and path | Request / behavior |
| --- | --- |
| `POST /api/auth/token` | JSON body `{"userName":"...","password":"..."}`; returns access/refresh tokens, expiration timestamps, roles, and permissions. Invalid credentials return 401. |
| `POST /api/auth/refresh` | JSON body `{"refreshToken":"..."}`; redeems a refresh token and returns a replacement token pair. |
| `GET /api/auth/verify` | Requires `Authorization: Bearer <access-token>` and returns the authenticated user, roles, permissions, and expiry. |

The token endpoint is `/api/auth/token`; there is no `/login` route. Token response JSON uses `accessToken`, `refreshToken`, `accessTokenExpiresAtUtc`, `refreshTokenExpiresAtUtc`, `roles`, and `permissions`. Do not paste issued tokens into documentation, tickets, or source control.

Refresh-token state is held in memory and redeemed tokens are revoked, so refresh state does not survive service restarts. The JWT signing configuration is shared development configuration and is not suitable for production. See the [API Gateway README](../ApiGateway/README.md) for gateway routes and the T16 smoke workflow.