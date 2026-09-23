using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace CartService.Api.Middleware;

public sealed partial class AccessTokenLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AccessTokenLoggingMiddleware> _logger;

    public AccessTokenLoggingMiddleware(RequestDelegate next, ILogger<AccessTokenLoggingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return InvokeCoreAsync(context);
    }

    private async Task InvokeCoreAsync(HttpContext context)
    {
        string authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            string token = authorization[7..].Trim();
            try
            {
                JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                Log.AccessTokenDetails(
                    _logger,
                    jwt.Claims.FirstOrDefault(claim => claim.Type == "sub")?.Value,
                    jwt.Claims.FirstOrDefault(claim => claim.Type == "name")?.Value,
                    string.Join(',', jwt.Claims.Where(claim => claim.Type == "role").Select(claim => claim.Value)),
                    string.Join(',', jwt.Claims.Where(claim => claim.Type == "permission").Select(claim => claim.Value)),
                    jwt.ValidTo,
                    jwt.Claims.FirstOrDefault(claim => claim.Type == "jti")?.Value);
            }
            catch (ArgumentException exception)
            {
                Log.UnableToParseAccessToken(_logger, exception);
            }
            catch (SecurityTokenException exception)
            {
                Log.UnableToParseAccessToken(_logger, exception);
            }
            catch (FormatException exception)
            {
                Log.UnableToParseAccessToken(_logger, exception);
            }
        }

        await _next(context).ConfigureAwait(false);
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Access token details: sub={Subject}, name={Name}, roles={Roles}, permissions={Permissions}, exp={ExpiresAt}, jti={Jti}")]
        public static partial void AccessTokenDetails(ILogger logger, string? subject, string? name, string roles, string permissions, DateTime expiresAt, string? jti);

        [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Unable to parse access token from request.")]
        public static partial void UnableToParseAccessToken(ILogger logger, Exception exception);
    }
}
