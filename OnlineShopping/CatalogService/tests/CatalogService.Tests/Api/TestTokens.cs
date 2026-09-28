using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using ShoppingAuth;

namespace CatalogService.Tests.Api;

/// <summary>
/// Issues access tokens the same way IdentityService does, so the API can be called without running Identity.
/// </summary>
internal static class TestTokens
{
    public static string Manager => Create(AuthRoles.Manager);

    public static string StoreCustomer => Create(AuthRoles.StoreCustomer);

    public static string Create(string role, string signingKey = AuthDefaults.SigningKey) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = AuthDefaults.Issuer,
        Audience = AuthDefaults.Audience,
        Expires = DateTime.UtcNow.Add(AuthDefaults.AccessTokenLifetime),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256),
        Claims = new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString(),
            [AuthClaimTypes.Name] = "test-user",
            [AuthClaimTypes.Role] = role,
        },
    });
}
