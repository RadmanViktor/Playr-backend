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
}
