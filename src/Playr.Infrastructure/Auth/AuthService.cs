using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Auth;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Auth;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    PlayrDbContext dbContext,
    JwtTokenGenerator tokenGenerator) : IAuthService
{
    private const string LoginFailureMessage = "Invalid username/email or password.";

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

        return new AuthUserDto(user.Id, user.Email!, user.UserName!, profile.DisplayName);
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

        return tokenGenerator.Generate(user);
    }

    public async Task<AuthUserDto?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null || user.Profile is null
            ? null
            : new AuthUserDto(user.Id, user.Email!, user.UserName!, user.Profile.DisplayName);
    }
}
