using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Security.Claims;

using IdentityService.Api.Controllers;
using IdentityService.Api.Models;
using IdentityService.Api.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace IdentityService.Tests;

public class IdentityServiceTests
{
    [Fact]
    public void ValidateCredentials_ReturnsUserForKnownAccount()
    {
        var store = new IdentityStore();

        IdentityUser? user = store.ValidateCredentials("manager@shop.local", "Manager123!");

        Assert.NotNull(user);
        Assert.Equal("manager@shop.local", user!.UserName);
        Assert.Contains("Manager", user.Roles);
    }

    [Fact]
    public void ValidateCredentials_ReturnsNullForInvalidPassword()
    {
        var store = new IdentityStore();

        IdentityUser? user = store.ValidateCredentials("manager@shop.local", "wrong");

        Assert.Null(user);
    }

    [Fact]
    public void IssueRefreshToken_ReturnsRedeemableToken()
    {
        var store = new IdentityStore();

        RefreshTokenRecord issued = store.IssueRefreshToken("manager@shop.local");

        Assert.Equal("manager@shop.local", issued.UserName);
        Assert.NotNull(issued.TokenHash);
        Assert.True(issued.ExpiresAtUtc > DateTimeOffset.UtcNow);

        RefreshTokenRecord? redeemed = store.RedeemRefreshToken(issued.TokenHash);

        Assert.NotNull(redeemed);
        Assert.Equal(issued.UserName, redeemed!.UserName);
        Assert.Equal(issued.TokenHash, redeemed.TokenHash);
        Assert.Null(store.RedeemRefreshToken(issued.TokenHash));
    }

    [Fact]
    public void RedeemRefreshToken_ReturnsNullForUnknownToken()
    {
        var store = new IdentityStore();

        RefreshTokenRecord? redeemed = store.RedeemRefreshToken("unknown-token");

        Assert.Null(redeemed);
    }

    [Fact]
    public void CreateTokens_ForManagerIncludesExpectedPermissions()
    {
        var store = new IdentityStore();
        var service = new TokenService(store);
        IdentityUser user = store.ValidateCredentials("manager@shop.local", "Manager123!")!;

        AuthTokenResponse response = service.CreateTokens(user);
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);

        Assert.Contains("Manager", response.Roles);
        Assert.Equal(new[] { "Create", "Delete", "Read", "Update" }, response.Permissions);
        Assert.Equal("manager@shop.local", token.Subject);
        Assert.Equal("Catalog Manager", token.Claims.First(claim => claim.Type == "name").Value);
        Assert.Contains(token.Claims.Where(claim => claim.Type == "role").Select(claim => claim.Value), role => role == "Manager");
        Assert.Contains(token.Claims.Where(claim => claim.Type == "permission").Select(claim => claim.Value), permission => permission == "Read");
        Assert.True(response.AccessTokenExpiresAtUtc > DateTimeOffset.UtcNow);
        Assert.True(response.RefreshTokenExpiresAtUtc > DateTimeOffset.UtcNow);
        Assert.False(string.IsNullOrWhiteSpace(response.RefreshToken));
    }

    [Fact]
    public void RefreshTokens_IssuesNewTokensForKnownUser()
    {
        var store = new IdentityStore();
        var service = new TokenService(store);
        RefreshTokenRecord refreshToken = store.IssueRefreshToken("customer@shop.local");

        AuthTokenResponse response = service.RefreshTokens(refreshToken);

        Assert.Equal(new[] { "Store customer" }, response.Roles);
        Assert.Contains("Read", response.Permissions);
        Assert.NotEqual(refreshToken.TokenHash, response.RefreshToken);
    }

    [Fact]
    public void RefreshTokens_ThrowsForUnknownUser()
    {
        var service = new TokenService(new IdentityStore());
        var refreshToken = new RefreshTokenRecord("missing@shop.local", "token", DateTimeOffset.UtcNow.AddMinutes(5));

        Assert.Throws<InvalidOperationException>(() => service.RefreshTokens(refreshToken));
    }

    [Fact]
    public void CreateToken_ReturnsUnauthorizedForInvalidCredentials()
    {
        var controller = new AuthController(new IdentityStore(), new TokenService(new IdentityStore()));

        ActionResult<AuthTokenResponse> result = controller.CreateToken(new LoginRequest("manager@shop.local", "wrong"));

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public void CreateToken_ReturnsOkForValidCredentials()
    {
        var store = new IdentityStore();
        var controller = new AuthController(store, new TokenService(store));

        ActionResult<AuthTokenResponse> result = controller.CreateToken(new LoginRequest("manager@shop.local", "Manager123!"));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuthTokenResponse>(ok.Value);
        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
    }

    [Fact]
    public void RefreshToken_ReturnsUnauthorizedForInvalidToken()
    {
        var controller = new AuthController(new IdentityStore(), new TokenService(new IdentityStore()));

        ActionResult<AuthTokenResponse> result = controller.RefreshToken(new RefreshTokenRequest("invalid"));

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    [Fact]
    public void RefreshToken_ReturnsOkForValidToken()
    {
        var store = new IdentityStore();
        var controller = new AuthController(store, new TokenService(store));
        RefreshTokenRecord refreshToken = store.IssueRefreshToken("customer@shop.local");

        ActionResult<AuthTokenResponse> result = controller.RefreshToken(new RefreshTokenRequest(refreshToken.TokenHash));

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AuthTokenResponse>(ok.Value);
        Assert.Contains("Store customer", response.Roles);
    }

    [Fact]
    public void Verify_UsesPrincipalClaims()
    {
        var controller = new AuthController(new IdentityStore(), new TokenService(new IdentityStore()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    new[]
                    {
                        new Claim("name", "Catalog Manager"),
                        new Claim("role", "Manager"),
                        new Claim("role", "Manager"),
                        new Claim("permission", "Read"),
                        new Claim("permission", "Read"),
                        new Claim("exp", new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture))
                    },
                    "Bearer"))
            }
        };

        ActionResult<TokenVerificationResponse> result = controller.Verify();

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<TokenVerificationResponse>(ok.Value);
        Assert.Equal("Catalog Manager", response.UserName);
        Assert.Equal(new[] { "Manager" }, response.Roles);
        Assert.Equal(new[] { "Read" }, response.Permissions);
        Assert.Equal("Bearer", response.AuthenticationType);
        Assert.Equal(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), response.ExpiresAtUtc);
    }

    [Fact]
    public void FromPrincipal_FallsBackToIdentityNameAndAuthenticationType()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.Name, "customer@shop.local"),
                new Claim("role", "StoreCustomer")
            },
            authenticationType: null,
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role));

        TokenVerificationResponse response = TokenVerificationResponse.FromPrincipal(principal);

        Assert.Equal("customer@shop.local", response.UserName);
        Assert.Equal("Bearer", response.AuthenticationType);
        Assert.Equal(new[] { "StoreCustomer" }, response.Roles);
    }
}
