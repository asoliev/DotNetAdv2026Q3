using System.Text;

using Microsoft.IdentityModel.Tokens;

namespace ShoppingAuth;

public static class AuthDefaults
{
    public const string Issuer = "DotNetAdv2026Q3.Identity";

    public const string Audience = "DotNetAdv2026Q3.OnlineShopping";

    public const string SigningKey = "DotNetAdv2026Q3-OnlineShopping-Identity-SigningKey-ChangeMe";

    public static TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(15);

    public static TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(7);

    public static SymmetricSecurityKey GetSigningKey() => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
}
