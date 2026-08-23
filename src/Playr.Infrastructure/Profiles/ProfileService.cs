using Microsoft.EntityFrameworkCore;
using Playr.Application.Profiles;
using Playr.Domain.Games;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Profiles;

public sealed class ProfileService(PlayrDbContext dbContext) : IProfileService
{
    private const int MaxListItems = 20;
    private const int MaxListItemLength = 64;
    private const int MaxExternalLinks = 10;
    private const int MaxExternalLinkKeyLength = 64;
    private const int MaxExternalLinkValueLength = 500;
    private const int MaxDisplayNameLength = 64;
    private const int MaxBioLength = 500;
    private const int MaxAvatarUrlLength = 500;
    private const int MaxRegionLength = 64;

    public async Task<ProfileDto?> GetByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = username.ToUpperInvariant();
        var profile = await dbContext.UserProfiles.AsNoTracking()
            .Include(p => p.LookingForGame)
            .FirstOrDefaultAsync(p => p.Username.ToUpper() == normalized, cancellationToken);
        return profile is null ? null : ToDto(profile);
    }

    public async Task<ProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles.AsNoTracking()
            .Include(p => p.LookingForGame)
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile is null ? null : ToDto(profile);
    }

    public async Task<ProfileDto> UpdateCurrentUserAsync(Guid userId, UpdateProfileCommand command, CancellationToken cancellationToken)
    {
        var displayName = command.DisplayName.Trim();
        if (displayName.Length == 0)
        {
            throw new InvalidOperationException("Display name is required.");
        }

        if (displayName.Length > MaxDisplayNameLength)
        {
            throw new InvalidOperationException($"Display name cannot be longer than {MaxDisplayNameLength} characters.");
        }

        var languages = NormalizeList(command.Languages, nameof(command.Languages));
        var platforms = NormalizeList(command.Platforms, nameof(command.Platforms));
        var currentlyPlayingGames = NormalizeList(command.CurrentlyPlayingGames, nameof(command.CurrentlyPlayingGames));
        var externalLinks = NormalizeExternalLinks(command.ExternalLinks);
        var avatarUrl = NormalizeOptionalHttpUrl(command.AvatarUrl, "Avatar URL", MaxAvatarUrlLength);
        var bio = NormalizeOptionalText(command.Bio, "Bio", MaxBioLength);
        var region = NormalizeOptionalText(command.Region, "Region", MaxRegionLength);

        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Profile was not found.");

        profile.DisplayName = displayName;
        profile.Bio = bio;
        profile.AvatarUrl = avatarUrl;
        profile.Region = region;
        profile.Languages = languages;
        profile.Platforms = platforms;
        profile.ExternalLinks = externalLinks;
        profile.CurrentlyPlayingGames = currentlyPlayingGames;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(profile);
    }

    public async Task<ProfileDto> UpdateStatusAsync(Guid userId, UpdateStatusCommand command, CancellationToken cancellationToken)
    {
        if (command.Status == ProfileStatus.LookingForGame)
        {
            if (command.LookingForGameId is null)
            {
                throw new InvalidOperationException("A game is required when status is Looking for game.");
            }

            if (command.LookingForPlayStyle is null)
            {
                throw new InvalidOperationException("A play style is required when status is Looking for game.");
            }

            var gameExists = await dbContext.Games.AsNoTracking()
                .AnyAsync(g => g.Id == command.LookingForGameId, cancellationToken);
            if (!gameExists)
            {
                throw new InvalidOperationException("The selected game was not found.");
            }
        }

        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Profile was not found.");

        profile.Status = command.Status;
        if (command.Status == ProfileStatus.LookingForGame)
        {
            profile.LookingForGameId = command.LookingForGameId;
            profile.LookingForPlayStyle = command.LookingForPlayStyle;
        }
        else
        {
            profile.LookingForGameId = null;
            profile.LookingForPlayStyle = null;
        }

        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        var reloaded = await dbContext.UserProfiles.AsNoTracking()
            .Include(p => p.LookingForGame)
            .FirstAsync(p => p.UserId == userId, cancellationToken);
        return ToDto(reloaded);
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
        profile.Status,
        profile.LookingForGameId,
        profile.LookingForGame?.Name,
        profile.LookingForPlayStyle,
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

        if (values.Count > MaxListItems)
        {
            throw new InvalidOperationException($"{name} cannot contain more than {MaxListItems} items.");
        }

        if (values.Any(value => value.Trim().Length > MaxListItemLength))
        {
            throw new InvalidOperationException($"{name} items cannot be longer than {MaxListItemLength} characters.");
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

        if (externalLinks.Count > MaxExternalLinks)
        {
            throw new InvalidOperationException($"External links cannot contain more than {MaxExternalLinks} items.");
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in externalLinks)
        {
            var key = pair.Key.Trim();
            var value = pair.Value.Trim();
            if (key.Length == 0)
            {
                throw new InvalidOperationException("External link keys are required.");
            }

            if (value.Length == 0)
            {
                throw new InvalidOperationException("External link values are required.");
            }

            if (key.Length > MaxExternalLinkKeyLength)
            {
                throw new InvalidOperationException($"External link keys cannot be longer than {MaxExternalLinkKeyLength} characters.");
            }

            if (value.Length > MaxExternalLinkValueLength)
            {
                throw new InvalidOperationException($"External link values cannot be longer than {MaxExternalLinkValueLength} characters.");
            }

            if (!IsAbsoluteHttpUrl(value))
            {
                throw new InvalidOperationException("External link values must be absolute HTTP or HTTPS URLs.");
            }

            if (!normalized.TryAdd(key, value))
            {
                throw new InvalidOperationException("External links cannot contain duplicate keys.");
            }
        }

        return normalized.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static string? NormalizeOptionalHttpUrl(string? value, string name, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"{name} cannot be longer than {maxLength} characters.");
        }

        if (!IsAbsoluteHttpUrl(trimmed))
        {
            throw new InvalidOperationException($"{name} must be an absolute HTTP or HTTPS URL.");
        }

        return trimmed;
    }

    private static string? NormalizeOptionalText(string? value, string name, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"{name} cannot be longer than {maxLength} characters.");
        }

        return trimmed;
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public async Task<IReadOnlyList<ProfileSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
            return [];

        var upper = trimmed.ToUpperInvariant();
        return await dbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.Username.ToUpper().Contains(upper) || p.DisplayName.ToUpper().Contains(upper))
            .OrderBy(p => p.Username)
            .Take(8)
            .Select(p => new ProfileSearchResult(p.UserId, p.Username, p.DisplayName, p.AvatarUrl))
            .ToListAsync(cancellationToken);
    }
}
