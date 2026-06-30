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
        var languages = NormalizeList(command.Languages, nameof(command.Languages));
        var platforms = NormalizeList(command.Platforms, nameof(command.Platforms));
        var currentlyPlayingGames = NormalizeList(command.CurrentlyPlayingGames, nameof(command.CurrentlyPlayingGames));
        var externalLinks = NormalizeExternalLinks(command.ExternalLinks);

        if (currentlyPlayingGames.Count > 20)
        {
            throw new InvalidOperationException("Currently playing games cannot contain more than 20 items.");
        }

        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Profile was not found.");

        profile.DisplayName = command.DisplayName.Trim();
        profile.Bio = command.Bio?.Trim();
        profile.AvatarUrl = command.AvatarUrl?.Trim();
        profile.Region = command.Region?.Trim();
        profile.Languages = languages;
        profile.Platforms = platforms;
        profile.ExternalLinks = externalLinks;
        profile.CurrentlyPlayingGames = currentlyPlayingGames;
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

    private static List<string> NormalizeList(IReadOnlyList<string>? values, string name)
    {
        if (values is null)
        {
            throw new InvalidOperationException($"{name} is required.");
        }

        if (values.Any(value => value is null))
        {
            throw new InvalidOperationException($"{name} cannot contain null values.");
        }

        return values.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct().ToList();
    }

    private static Dictionary<string, string> NormalizeExternalLinks(IReadOnlyDictionary<string, string>? externalLinks)
    {
        if (externalLinks is null)
        {
            throw new InvalidOperationException("External links are required.");
        }

        if (externalLinks.Any(pair => pair.Key is null || pair.Value is null))
        {
            throw new InvalidOperationException("External links cannot contain null keys or values.");
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in externalLinks)
        {
            var key = pair.Key.Trim();
            var value = pair.Value.Trim();
            if (key.Length == 0)
            {
                continue;
            }

            if (!normalized.TryAdd(key, value))
            {
                throw new InvalidOperationException("External links cannot contain duplicate keys.");
            }
        }

        return normalized.ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}
