using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Playr.Application.Auth;
using Playr.Application.Badges;
using Playr.Application.Email;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Auth;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    PlayrDbContext dbContext,
    JwtTokenGenerator tokenGenerator,
    IEmailSender emailSender,
    IOptions<FrontendOptions> frontendOptions,
    IOptions<AuthOptions> authOptions,
    IBadgeService badgeService,
    ILogger<AuthService> logger) : IAuthService
{
    private const string LoginFailureMessage = "Invalid username/email or password.";
    private const string EmailNotConfirmedMessage = "Please confirm your email address before logging in.";

    public async Task<AuthUserDto> RegisterAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var usernameExists = await userManager.Users.AnyAsync(u => u.NormalizedUserName == command.Username.ToUpperInvariant(), cancellationToken);
        if (usernameExists)
        {
            throw new InvalidOperationException("Username already exists.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = command.Email,
            UserName = command.Username
        };

        var result = await userManager.CreateAsync(user, command.Password);
        if (!result.Succeeded)
        {
            var errorMessage = string.Join(" ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(errorMessage) ? "Registration failed." : errorMessage);
        }

        var profile = new UserProfile
        {
            UserId = user.Id,
            Username = command.Username,
            DisplayName = command.Username
        };

        dbContext.UserProfiles.Add(profile);

        if (authOptions.Value.AutoConfirmEmailOnRegister)
        {
            user.EmailConfirmed = true;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            await userManager.DeleteAsync(user);
            throw;
        }

        try
        {
            await badgeService.CheckFirstHundredUsersAsync(user.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            // Best-effort: a badge-unlock failure must never fail registration itself.
            logger.LogError(ex, "Failed to evaluate first-100-users badge for user {UserId}.", user.Id);
        }

        if (authOptions.Value.AutoConfirmEmailOnRegister)
        {
            // Local development: skip the confirmation email entirely since the account
            // is already confirmed, so there's no link for the user to click.
            return ToDto(user, profile.DisplayName);
        }

        // The account exists at this point; a failed email must not undo registration.
        // The user can request a new confirmation email instead.
        await SendConfirmationEmailAsync(user, cancellationToken);

        return ToDto(user, profile.DisplayName);
    }

    public async Task<AuthResult> LoginAsync(string usernameOrEmail, string password, CancellationToken cancellationToken)
    {
        var normalized = usernameOrEmail.ToUpperInvariant();
        var user = await userManager.Users.FirstOrDefaultAsync(
            u => u.NormalizedUserName == normalized || u.NormalizedEmail == normalized,
            cancellationToken);

        if (user is null)
        {
            throw new UnauthorizedAccessException(LoginFailureMessage);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            throw new UnauthorizedAccessException(LoginFailureMessage);
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            throw new UnauthorizedAccessException(LoginFailureMessage);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        // Checked only after the password is verified so the response cannot be used
        // to discover which email addresses are registered.
        if (!user.EmailConfirmed)
        {
            throw new EmailNotConfirmedException(EmailNotConfirmedMessage);
        }

        return await CreateSessionAsync(user, cancellationToken);
    }

    public async Task<AuthResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = HashRefreshToken(refreshToken);
        var now = DateTimeOffset.UtcNow;
        var currentSession = await dbContext.RefreshSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(session => session.TokenHash == tokenHash, cancellationToken);

        if (currentSession is null)
        {
            throw new UnauthorizedAccessException("The session is invalid or has expired.");
        }

        if (currentSession.RevokedAt is not null)
        {
            if (currentSession.ReplacedByTokenHash is not null)
            {
                await RevokeFamilyAsync(currentSession.FamilyId, now, "reused", cancellationToken);
            }

            throw new UnauthorizedAccessException("The session is invalid or has expired.");
        }

        if (currentSession.ExpiresAt <= now || currentSession.AbsoluteExpiresAt <= now)
        {
            await dbContext.RefreshSessions
                .Where(session => session.Id == currentSession.Id && session.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(session => session.RevokedAt, now)
                    .SetProperty(session => session.RevocationReason, "expired"), cancellationToken);
            throw new UnauthorizedAccessException("The session is invalid or has expired.");
        }

        var user = await userManager.Users.SingleOrDefaultAsync(user => user.Id == currentSession.UserId, cancellationToken);
        if (user is null || !user.EmailConfirmed || await userManager.IsLockedOutAsync(user))
        {
            await RevokeFamilyAsync(currentSession.FamilyId, now, "user-invalid", cancellationToken);
            throw new UnauthorizedAccessException("The session is invalid or has expired.");
        }

        var replacementToken = GenerateRefreshToken();
        var replacementHash = HashRefreshToken(replacementToken);
        var replacementExpiresAt = Min(
            now.AddDays(authOptions.Value.RefreshTokenExpirationDays),
            currentSession.AbsoluteExpiresAt);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var updated = await dbContext.RefreshSessions
            .Where(session =>
                session.Id == currentSession.Id &&
                session.RevokedAt == null &&
                session.ExpiresAt > now &&
                session.AbsoluteExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(session => session.LastUsedAt, now)
                .SetProperty(session => session.RevokedAt, now)
                .SetProperty(session => session.RevocationReason, "rotated")
                .SetProperty(session => session.ReplacedByTokenHash, replacementHash), cancellationToken);

        if (updated == 0)
        {
            await dbContext.RefreshSessions
                .Where(session => session.FamilyId == currentSession.FamilyId && session.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(session => session.RevokedAt, now)
                    .SetProperty(session => session.RevocationReason, "reused"), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            throw new UnauthorizedAccessException("The session is invalid or has expired.");
        }

        dbContext.RefreshSessions.Add(new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FamilyId = currentSession.FamilyId,
            TokenHash = replacementHash,
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = replacementExpiresAt,
            AbsoluteExpiresAt = currentSession.AbsoluteExpiresAt,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.RefreshSessions
            .Where(session =>
                session.FamilyId == currentSession.FamilyId &&
                session.RevocationReason == "rotated" &&
                session.RevokedAt < now.AddDays(-1))
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return tokenGenerator.Generate(user) with
        {
            RefreshToken = replacementToken,
            RefreshTokenExpiresAt = replacementExpiresAt,
        };
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var tokenHash = HashRefreshToken(refreshToken);
        var now = DateTimeOffset.UtcNow;
        await dbContext.RefreshSessions
            .Where(session => session.TokenHash == tokenHash && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(session => session.RevokedAt, now)
                .SetProperty(session => session.RevocationReason, "logout"), cancellationToken);
    }

    public async Task<AuthUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null || user.Profile is null
            ? null
            : ToDto(user, user.Profile.DisplayName);
    }

    public async Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        if (user.EmailConfirmed)
        {
            // Treat a repeated click on the same link as success.
            return true;
        }

        if (!TryDecodeToken(token, out var decodedToken))
        {
            return false;
        }

        var result = await userManager.ConfirmEmailAsync(user, decodedToken);
        if (!result.Succeeded)
        {
            logger.LogInformation("Email confirmation failed for user {UserId}.", userId);
        }

        return result.Succeeded;
    }

    public async Task ResendConfirmationAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.ToUpperInvariant();
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);

        if (user is null || user.EmailConfirmed)
        {
            // Silently succeed so the endpoint cannot be used to enumerate accounts.
            return;
        }

        await SendConfirmationEmailAsync(user, cancellationToken);
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = email.ToUpperInvariant();
        var user = await userManager.Users.FirstOrDefaultAsync(u => u.NormalizedEmail == normalized, cancellationToken);

        if (user is null)
        {
            // Silently succeed so the endpoint cannot be used to enumerate accounts.
            return;
        }

        try
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var encodedToken = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token));

            var baseUrl = frontendOptions.Value.BaseUrl.TrimEnd('/');
            var resetUrl = $"{baseUrl}/reset-password?userId={user.Id}&token={encodedToken}";

            await emailSender.SendAsync(
                user.Email!,
                EmailTemplates.PasswordResetSubject,
                EmailTemplates.PasswordResetBody(user.UserName!, resetUrl),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send password reset email to user {UserId}.", user.Id);
        }
    }

    public async Task<bool> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var user = await userManager.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        if (!TryDecodeToken(token, out var decodedToken))
        {
            return false;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await userManager.ResetPasswordAsync(user, decodedToken, newPassword);
        if (!result.Succeeded)
        {
            logger.LogInformation("Password reset failed for user {UserId}.", userId);
        }

        if (result.Succeeded)
        {
            var now = DateTimeOffset.UtcNow;
            await dbContext.RefreshSessions
                .Where(session => session.UserId == user.Id && session.RevokedAt == null)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(session => session.RevokedAt, now)
                    .SetProperty(session => session.RevocationReason, "password-reset"), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return result.Succeeded;
    }

    private async Task<AuthResult> CreateSessionAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await dbContext.RefreshSessions
            .Where(session => session.AbsoluteExpiresAt <= now)
            .ExecuteDeleteAsync(cancellationToken);
        var refreshToken = GenerateRefreshToken();
        var absoluteExpiresAt = now.AddDays(authOptions.Value.RefreshTokenAbsoluteExpirationDays);
        var expiresAt = Min(now.AddDays(authOptions.Value.RefreshTokenExpirationDays), absoluteExpiresAt);

        dbContext.RefreshSessions.Add(new RefreshSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FamilyId = Guid.NewGuid(),
            TokenHash = HashRefreshToken(refreshToken),
            CreatedAt = now,
            LastUsedAt = now,
            ExpiresAt = expiresAt,
            AbsoluteExpiresAt = absoluteExpiresAt,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return tokenGenerator.Generate(user) with
        {
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = expiresAt,
        };
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken)
    {
        await dbContext.RefreshSessions
            .Where(session => session.FamilyId == familyId && session.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(session => session.RevokedAt, revokedAt)
                .SetProperty(session => session.RevocationReason, reason), cancellationToken);
    }

    private static string GenerateRefreshToken()
    {
        return Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
    }

    private static string HashRefreshToken(string refreshToken)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right)
    {
        return left <= right ? left : right;
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        try
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var encodedToken = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token));

            var baseUrl = frontendOptions.Value.BaseUrl.TrimEnd('/');
            var confirmationUrl = $"{baseUrl}/confirm-email?userId={user.Id}&token={encodedToken}";

            await emailSender.SendAsync(
                user.Email!,
                EmailTemplates.ConfirmationSubject,
                EmailTemplates.ConfirmationBody(user.UserName!, confirmationUrl),
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send confirmation email to user {UserId}.", user.Id);
        }
    }

    private static bool TryDecodeToken(string token, out string decoded)
    {
        try
        {
            decoded = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(token));
            return true;
        }
        catch (FormatException)
        {
            decoded = string.Empty;
            return false;
        }
    }

    private static AuthUserDto ToDto(ApplicationUser user, string displayName)
    {
        return new AuthUserDto(user.Id, user.Email!, user.UserName!, displayName, user.EmailConfirmed);
    }
}
