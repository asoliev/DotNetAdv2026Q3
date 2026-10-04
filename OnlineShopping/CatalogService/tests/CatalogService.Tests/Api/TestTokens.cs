using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using ShoppingAuth;

namespace CatalogService.Tests.Api;

/// <summary>
/// Issues access tokens the same way IdentityService does, so the API can be called without running Identity.
/// </summary>
internal static class TestTokens
{
    public static string Admin => Create(AuthRoles.Admin);

    public static string Manager => Create(AuthRoles.Manager);

    public static string StoreCustomer => Create(AuthRoles.StoreCustomer);

    public static string ExpiredManager => Create(AuthRoles.Manager, expires: DateTime.UtcNow.AddMinutes(-5));

    public static string WrongIssuerManager => Create(AuthRoles.Manager, issuer: "wrong-issuer");

    public static string WrongAudienceManager => Create(AuthRoles.Manager, audience: "wrong-audience");

    public static string Create(string role, string signingKey = AuthDefaults.SigningKey, string issuer = AuthDefaults.Issuer, string audience = AuthDefaults.Audience, DateTime? expires = null) => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
    {
        Issuer = issuer,
        Audience = audience,
        Expires = expires ?? DateTime.UtcNow.Add(AuthDefaults.AccessTokenLifetime),
        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256),
        Claims = new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString(),
            [AuthClaimTypes.Name] = "test-user",
            [AuthClaimTypes.Role] = role,
        },
    });
}
