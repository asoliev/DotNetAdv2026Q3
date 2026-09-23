using System.Security.Claims;

using ShoppingAuth;

namespace IdentityService.Api.Models;

internal sealed record LoginRequest(string UserName, string Password);

internal sealed record RefreshTokenRequest(string RefreshToken);

internal sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

internal sealed record TokenVerificationResponse(
    string UserName,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    DateTimeOffset? ExpiresAtUtc,
    string AuthenticationType)
{
    public static TokenVerificationResponse FromPrincipal(ClaimsPrincipal principal)
    {
        var roles = principal.FindAll(AuthClaimTypes.Role).Select(claim => claim.Value).Distinct().ToList();
        var permissions = principal.FindAll(AuthClaimTypes.Permission).Select(claim => claim.Value).Distinct().ToList();

        DateTimeOffset? expiresAt = principal.FindFirst("exp")?.Value is { } expiresValue &&
                                    long.TryParse(expiresValue, out var unixSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
            : null;

        return new TokenVerificationResponse(
            principal.FindFirstValue(AuthClaimTypes.Name) ?? principal.Identity?.Name ?? string.Empty,
            roles,
            permissions,
            expiresAt,
            principal.Identity?.AuthenticationType ?? "Bearer");
    }
}

internal sealed record IdentityUser(string UserName, string Password, string DisplayName, IReadOnlyList<string> Roles);

internal sealed record RefreshTokenRecord(string UserName, string TokenHash, DateTimeOffset ExpiresAtUtc, bool IsRevoked = false)
{
    public RefreshTokenRecord Revoke() => this with { IsRevoked = true };
}
