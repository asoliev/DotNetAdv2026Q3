using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

using IdentityService.Api.Models;

using ShoppingAuth;

namespace IdentityService.Api.Services;

internal sealed class IdentityStore
{
    private readonly ConcurrentDictionary<string, IdentityUser> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RefreshTokenRecord> _refreshTokens = new(StringComparer.Ordinal);

    public IdentityStore()
    {
        var manager = new IdentityUser("manager@shop.local", "Manager123!", "Catalog Manager", new[] { AuthRoles.Manager });
        var customer = new IdentityUser("customer@shop.local", "Customer123!", "Store Customer", new[] { AuthRoles.StoreCustomer });

        _users[manager.UserName] = manager;
        _users[customer.UserName] = customer;
    }

    public IdentityUser? ValidateCredentials(string userName, string password) => _users.TryGetValue(userName, out IdentityUser? user) && string.Equals(user.Password, password, StringComparison.Ordinal)
            ? user
            : null;

    public RefreshTokenRecord IssueRefreshToken(string userName)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var refreshToken = new RefreshTokenRecord(userName, HashToken(rawToken), DateTimeOffset.UtcNow.Add(AuthDefaults.RefreshTokenLifetime));
        _refreshTokens[refreshToken.TokenHash] = refreshToken;
        return refreshToken with { TokenHash = rawToken };
    }

    public RefreshTokenRecord? RedeemRefreshToken(string refreshToken)
    {
        var tokenHash = HashToken(refreshToken);
        if (!_refreshTokens.TryGetValue(tokenHash, out RefreshTokenRecord? storedToken))
        {
            return null;
        }

        if (storedToken.IsRevoked || storedToken.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            _refreshTokens.TryRemove(tokenHash, out _);
            return null;
        }

        _refreshTokens[tokenHash] = storedToken.Revoke();
        return storedToken with { TokenHash = refreshToken };
    }

    public IdentityUser? GetUser(string userName) => _users.TryGetValue(userName, out IdentityUser? user) ? user : null;

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
