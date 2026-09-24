using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ShoppingAuth;

public static class ShoppingJwtAuthenticationExtensions
{
    public static IServiceCollection AddShoppingJwtAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = AuthDefaults.Issuer,
                    ValidateAudience = true,
                    ValidAudience = AuthDefaults.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = AuthDefaults.GetSigningKey(),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = AuthClaimTypes.Name,
                    RoleClaimType = AuthClaimTypes.Role,
                };
            });

        services.AddAuthorization();

        return services;
    }
}
