using IdentityService.Api.Models;
using IdentityService.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IdentityStore _identityStore;
    private readonly TokenService _tokenService;

    public AuthController(IdentityStore identityStore, TokenService tokenService)
    {
        _identityStore = identityStore;
        _tokenService = tokenService;
    }

    [HttpPost("token")]
    public ActionResult<AuthTokenResponse> CreateToken([FromBody] LoginRequest request)
    {
        var user = _identityStore.ValidateCredentials(request.UserName, request.Password);
        if (user is null)
        {
            return Unauthorized();
        }

        var token = _tokenService.CreateTokens(user);
        return Ok(token);
    }

    [HttpPost("refresh")]
    public ActionResult<AuthTokenResponse> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var token = _identityStore.RedeemRefreshToken(request.RefreshToken);
        if (token is null)
        {
            return Unauthorized();
        }

        var refreshed = _tokenService.RefreshTokens(token);
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