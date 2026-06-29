using Microsoft.EntityFrameworkCore;
using Playr.Application.Profiles;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Profiles;

public sealed class ProfileService(PlayrDbContext dbContext) : IProfileService
{
    public async Task<ProfileDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = username.ToUpperInvariant();
        var profile = await dbContext.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Username.ToUpper() == normalized, cancellationToken);
        return profile is null ? null : ToDto(profile);
    }

    public async Task<ProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile is null ? null : ToDto(profile);
    }

    public async Task<ProfileDto> UpdateCurrentUserAsync(Guid userId, UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        if (command.CurrentlyPlayingGames.Count > 20)
        {
            throw new InvalidOperationException("Currently playing games cannot contain more than 20 items.");
        }

        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Profile was not found.");

        profile.DisplayName = command.DisplayName.Trim();
        profile.Bio = command.Bio?.Trim();
        profile.AvatarUrl = command.AvatarUrl?.Trim();
        profile.Region = command.Region?.Trim();
        profile.Languages = command.Languages.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct().ToList();
        profile.Platforms = command.Platforms.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct().ToList();
        profile.ExternalLinks = command.ExternalLinks.ToDictionary(pair => pair.Key.Trim(), pair => pair.Value.Trim());
        profile.CurrentlyPlayingGames = command.CurrentlyPlayingGames.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct().ToList();
        profile.LookingForPlayers = command.LookingForPlayers;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(profile);
    }

    private static ProfileDto ToDto(UserProfile profile) => new(
        profile.UserId,
        profile.Username,
        profile.DisplayName,
        profile.Bio,
        profile.AvatarUrl,
        profile.Region,
        profile.Languages,
        profile.Platforms,
        profile.ExternalLinks,
        profile.CurrentlyPlayingGames,
        profile.LookingForPlayers,
        profile.CreatedAt,
        profile.UpdatedAt);
}
