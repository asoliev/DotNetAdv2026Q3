using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using ShoppingAuth;

namespace ApiGateway.Tests;

public static class TestTokens
{
    public static string Admin => Create(AuthRoles.Admin);

    public static string Manager => Create(AuthRoles.Manager);

    public static string StoreCustomer => Create(AuthRoles.StoreCustomer);

    public static string Create(
        string role,
        string signingKey = AuthDefaults.SigningKey,
        string issuer = AuthDefaults.Issuer,
        string audience = AuthDefaults.Audience,
        DateTime? expires = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Expires = expires ?? DateTime.UtcNow.Add(AuthDefaults.AccessTokenLifetime),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = Guid.NewGuid().ToString(),
                [AuthClaimTypes.Name] = "gateway-test-user",
                [AuthClaimTypes.Role] = role,
            },
        });
    }
}