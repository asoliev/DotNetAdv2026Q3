using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using IdentityService.Api.Models;

using Microsoft.IdentityModel.Tokens;

using ShoppingAuth;

namespace IdentityService.Api.Services;

internal sealed class TokenService(IdentityStore identityStore)
{
    private readonly IdentityStore _identityStore = identityStore;

    public AuthTokenResponse CreateTokens(IdentityUser user)
    {
        var roles = user.Roles.Distinct().ToList();
        IReadOnlyList<string> permissions = GetPermissionsForRoles(roles);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DateTimeOffset accessTokenExpiresAt = now.Add(AuthDefaults.AccessTokenLifetime);
        RefreshTokenRecord refreshToken = _identityStore.IssueRefreshToken(user.UserName);

        var claims = new List<Claim>
        {
            new(AuthClaimTypes.Name, user.DisplayName),
            new(JwtRegisteredClaimNames.Sub, user.UserName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        claims.AddRange(roles.Select(role => new Claim(AuthClaimTypes.Role, role)));
        claims.AddRange(permissions.Select(permission => new Claim(AuthClaimTypes.Permission, permission)));

        var token = new JwtSecurityToken(
            issuer: AuthDefaults.Issuer,
            audience: AuthDefaults.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessTokenExpiresAt.UtcDateTime,
            signingCredentials: new SigningCredentials(AuthDefaults.GetSigningKey(), SecurityAlgorithms.HmacSha256));

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        return new AuthTokenResponse(
            accessToken,
            refreshToken.TokenHash,
            accessTokenExpiresAt,
            refreshToken.ExpiresAtUtc,
            roles,
            permissions);
    }

    public AuthTokenResponse RefreshTokens(RefreshTokenRecord refreshToken)
    {
        IdentityUser user = _identityStore.GetUser(refreshToken.UserName) ?? throw new InvalidOperationException("Unknown user for refresh token.");
        return CreateTokens(user);
    }

    private static IReadOnlyList<string> GetPermissionsForRoles(IEnumerable<string> roles)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            if (string.Equals(role, AuthRoles.Manager, StringComparison.OrdinalIgnoreCase))
            {
                permissions.Add(AuthPermissions.Read);
                permissions.Add(AuthPermissions.Create);
                permissions.Add(AuthPermissions.Update);
                permissions.Add(AuthPermissions.Delete);
            }

            if (string.Equals(role, AuthRoles.StoreCustomer, StringComparison.OrdinalIgnoreCase))
            {
                permissions.Add(AuthPermissions.Read);
            }
        }

        return permissions.OrderBy(permission => permission).ToList();
    }
}
