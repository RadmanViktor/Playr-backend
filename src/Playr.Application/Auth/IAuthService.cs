namespace Playr.Application.Auth;

public interface IAuthService
{
    Task<AuthUserDto> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken);
    Task<AuthResult> LoginAsync(string usernameOrEmail, string password, CancellationToken cancellationToken);
    Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    Task<AuthUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Confirms an email address. Returns false when the user or token is invalid or expired.
    /// </summary>
    Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a new confirmation email. Completes silently for unknown or already
    /// confirmed addresses to avoid disclosing which accounts exist.
    /// </summary>
    Task ResendConfirmationAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Sends a password reset email. Completes silently for unknown addresses to
    /// avoid disclosing which accounts exist.
    /// </summary>
    Task ForgotPasswordAsync(string email, CancellationToken cancellationToken);

    /// <summary>
    /// Resets a user's password using a token previously issued by
    /// <see cref="ForgotPasswordAsync"/>. Returns false when the user or token is
    /// invalid or expired, or the new password does not meet requirements.
    /// </summary>
    Task<bool> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken);
}
