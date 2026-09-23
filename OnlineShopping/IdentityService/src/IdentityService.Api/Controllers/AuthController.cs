using IdentityService.Api.Models;
using IdentityService.Api.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/auth")]
internal sealed class AuthController(IdentityStore identityStore, TokenService tokenService) : ControllerBase
{
    private readonly IdentityStore _identityStore = identityStore;
    private readonly TokenService _tokenService = tokenService;

    [HttpPost("token")]
    public ActionResult<AuthTokenResponse> CreateToken([FromBody] LoginRequest request)
    {
        IdentityUser? user = _identityStore.ValidateCredentials(request.UserName, request.Password);
        if (user is null)
        {
            return Unauthorized();
        }

        AuthTokenResponse token = _tokenService.CreateTokens(user);
        return Ok(token);
    }

    [HttpPost("refresh")]
    public ActionResult<AuthTokenResponse> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        RefreshTokenRecord? token = _identityStore.RedeemRefreshToken(request.RefreshToken);
        if (token is null)
        {
            return Unauthorized();
        }

        AuthTokenResponse refreshed = _tokenService.RefreshTokens(token);
        return Ok(refreshed);
    }

    [Authorize]
    [HttpGet("verify")]
    public ActionResult<TokenVerificationResponse> Verify()
    {
        var response = TokenVerificationResponse.FromPrincipal(User);
        return Ok(response);
    }
}
