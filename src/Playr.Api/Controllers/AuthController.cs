using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Auth;
using Playr.Application.Auth;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    private const string RefreshCookieName = "playr_refresh";
    private const string SessionHeaderName = "X-Playr-Session";

    [HttpPost("register")]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await authService.RegisterAsync(
                new RegisterUserCommand(request.Email, request.Username, request.Password),
                cancellationToken);

            return Ok(ToResponse(user));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await authService.LoginAsync(request.UsernameOrEmail, request.Password, cancellationToken);
            SetRefreshCookie(result);
            return Ok(new LoginResponse(result.AccessToken, result.ExpiresAt));
        }
        catch (EmailNotConfirmedException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message, code = "email_not_confirmed" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> Refresh(CancellationToken cancellationToken)
    {
        if (!HasSessionHeader())
        {
            return BadRequest(new { error = "The session header is required." });
        }

        if (!Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            ClearRefreshCookie();
            return Unauthorized(new { error = "The session is invalid or has expired." });
        }

        try
        {
            var result = await authService.RefreshAsync(refreshToken, cancellationToken);
            SetRefreshCookie(result);
            return Ok(new LoginResponse(result.AccessToken, result.ExpiresAt));
        }
        catch (UnauthorizedAccessException ex)
        {
            ClearRefreshCookie();
            return Unauthorized(new { error = ex.Message });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        if (!HasSessionHeader())
        {
            return BadRequest(new { error = "The session header is required." });
        }

        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) && !string.IsNullOrWhiteSpace(refreshToken))
        {
            await authService.RevokeRefreshTokenAsync(refreshToken, cancellationToken);
        }

        ClearRefreshCookie();
        return NoContent();
    }

    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        var confirmed = await authService.ConfirmEmailAsync(request.UserId, request.Token, cancellationToken);

        return confirmed
            ? Ok(new { confirmed = true })
            : BadRequest(new { error = "This confirmation link is invalid or has expired." });
    }

    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request, CancellationToken cancellationToken)
    {
        await authService.ResendConfirmationAsync(request.Email, cancellationToken);

        // Always reports success so the endpoint cannot reveal which accounts exist.
        return Ok(new { sent = true });
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await authService.ForgotPasswordAsync(request.Email, cancellationToken);

        // Always reports success so the endpoint cannot reveal which accounts exist.
        return Ok(new { sent = true });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var reset = await authService.ResetPasswordAsync(request.UserId, request.Token, request.NewPassword, cancellationToken);

        return reset
            ? Ok(new { reset = true })
            : BadRequest(new { error = "This password reset link is invalid or has expired." });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var user = await authService.GetCurrentUserAsync(userId, cancellationToken);
        return user is null ? Unauthorized() : Ok(ToResponse(user));
    }

    private static UserResponse ToResponse(AuthUserDto user)
    {
        return new UserResponse(user.Id, user.Email, user.Username, user.DisplayName, user.EmailConfirmed);
    }

    private bool HasSessionHeader()
    {
        return Request.Headers.TryGetValue(SessionHeaderName, out var values) && values.Contains("1");
    }

    private void SetRefreshCookie(AuthResult result)
    {
        if (string.IsNullOrWhiteSpace(result.RefreshToken) || result.RefreshTokenExpiresAt is null)
        {
            throw new InvalidOperationException("Authentication did not produce a refresh token.");
        }

        Response.Cookies.Append(RefreshCookieName, result.RefreshToken, CreateCookieOptions(result.RefreshTokenExpiresAt.Value));
    }

    private void ClearRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, CreateCookieOptions(DateTimeOffset.UnixEpoch));
    }

    private CookieOptions CreateCookieOptions(DateTimeOffset expiresAt)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/api/auth",
            Expires = expiresAt,
            IsEssential = true,
        };
    }
}
