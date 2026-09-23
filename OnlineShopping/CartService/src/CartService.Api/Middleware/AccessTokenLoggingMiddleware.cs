using System.IdentityModel.Tokens.Jwt;

namespace CartService.Api.Middleware;

internal sealed class AccessTokenLoggingMiddleware(RequestDelegate next, ILogger<AccessTokenLoggingMiddleware> logger)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger<AccessTokenLoggingMiddleware> _logger = logger;

    public async Task InvokeAsync(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authorization[7..].Trim();
            try
            {
                JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                _logger.LogInformation(
                    "Access token details: sub={Subject}, name={Name}, roles={Roles}, permissions={Permissions}, exp={ExpiresAt}, jti={Jti}",
                    jwt.Claims.FirstOrDefault(claim => claim.Type == "sub")?.Value,
                    jwt.Claims.FirstOrDefault(claim => claim.Type == "name")?.Value,
                    string.Join(',', jwt.Claims.Where(claim => claim.Type == "role").Select(claim => claim.Value)),
                    string.Join(',', jwt.Claims.Where(claim => claim.Type == "permission").Select(claim => claim.Value)),
                    jwt.ValidTo,
                    jwt.Claims.FirstOrDefault(claim => claim.Type == "jti")?.Value);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Unable to parse access token from request.");
            }
        }

        await _next(context).ConfigureAwait(false);
    }
}
