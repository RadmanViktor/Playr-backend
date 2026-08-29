using System.Buffers.Text;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Playr.Application.Auth;
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

        return tokenGenerator.Generate(user);
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
