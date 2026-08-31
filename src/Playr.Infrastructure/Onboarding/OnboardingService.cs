using Microsoft.EntityFrameworkCore;
using Playr.Application.Onboarding;
using Playr.Domain.Games;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Onboarding;

public sealed class OnboardingService(PlayrDbContext dbContext) : IOnboardingService
{
    private const int MaxListItems = 20;
    private const int MaxListItemLength = 64;
    private const int MaxBioLength = 500;
    private const int MaxStatusTextLength = 200;
    private const int MaxPlayingNowItems = 10;

    private static readonly HashSet<string> AllowedGenres = new(StringComparer.OrdinalIgnoreCase)
    {
        "FPS", "RPG", "Survival", "MMO", "Strategy", "Horror", "Racing", "Sports", "Co-op", "Indie",
    };

    private static readonly HashSet<string> AllowedTypicalPlayTimes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Evenings", "Weekends", "Daytime", "Varies",
    };

    public async Task<OnboardingStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken)
    {
        var hasCompleted = await dbContext.UserProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.HasCompletedOnboarding)
            .FirstOrDefaultAsync(cancellationToken);
        return new OnboardingStatusDto(hasCompleted);
    }

    public async Task<OnboardingStatusDto> CompleteAsync(Guid userId, CompleteOnboardingCommand command, CancellationToken cancellationToken)
    {
        var platforms = NormalizeList(command.Platforms, "Platforms");
        var genres = NormalizeList(command.Genres, "Genres");
        var invalidGenre = genres.FirstOrDefault(g => !AllowedGenres.Contains(g));
        if (invalidGenre is not null)
        {
            throw new InvalidOperationException($"'{invalidGenre}' is not a supported genre.");
        }

        var bio = NormalizeOptionalText(command.Bio, MaxBioLength);

        var typicalPlayTimes = NormalizeList(command.TypicalPlayTimes, "TypicalPlayTimes");
        var invalidPlayTime = typicalPlayTimes.FirstOrDefault(v => !AllowedTypicalPlayTimes.Contains(v));
        if (invalidPlayTime is not null)
        {
            throw new InvalidOperationException($"'{invalidPlayTime}' is not a supported typical play time.");
        }

        if (command.GameIds.Count > MaxListItems)
        {
            throw new InvalidOperationException($"You can only select up to {MaxListItems} games.");
        }

        if (command.PlayingNow.Count > MaxPlayingNowItems)
        {
            throw new InvalidOperationException($"You can only mark up to {MaxPlayingNowItems} games as playing now.");
        }

        var profile = await dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Profile was not found.");

        var gameIds = command.GameIds.Distinct().ToList();
        var playingNowGameIds = command.PlayingNow.Select(p => p.GameId).Distinct().ToList();
        var allGameIds = gameIds.Union(playingNowGameIds).ToList();

        if (allGameIds.Count > 0)
        {
            var existingGameCount = await dbContext.Games.CountAsync(g => allGameIds.Contains(g.Id), cancellationToken);
            if (existingGameCount != allGameIds.Count)
            {
                throw new InvalidOperationException("One or more selected games were not found.");
            }
        }

        profile.Platforms = platforms;
        profile.Genres = genres;
        profile.PlaystylePreference = command.PlaystylePreference;
        profile.UsuallyPlayingWith = command.UsuallyPlayingWith;
        profile.TypicalPlayTimes = typicalPlayTimes;
        if (bio is not null)
        {
            profile.Bio = bio;
        }

        profile.HasCompletedOnboarding = true;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        var existingLibraryGameIds = await dbContext.UserGameLibraryEntries
            .Where(e => e.UserId == userId)
            .Select(e => e.GameId)
            .ToListAsync(cancellationToken);
        var existingLibrarySet = existingLibraryGameIds.ToHashSet();

        var now = DateTimeOffset.UtcNow;
        foreach (var gameId in gameIds.Where(id => !existingLibrarySet.Contains(id)))
        {
            dbContext.UserGameLibraryEntries.Add(new UserGameLibraryEntry
            {
                UserId = userId,
                GameId = gameId,
                AddedAt = now,
                UpdatedAt = now,
            });
        }

        var existingPlayingNow = await dbContext.UserPlayingNows
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);
        var existingPlayingNowByGameId = existingPlayingNow.ToDictionary(p => p.GameId);

        foreach (var item in command.PlayingNow)
        {
            var statusText = NormalizeOptionalText(item.StatusText, MaxStatusTextLength);
            if (existingPlayingNowByGameId.TryGetValue(item.GameId, out var existing))
            {
                existing.StatusText = statusText;
                existing.UpdatedAt = now;
            }
            else
            {
                dbContext.UserPlayingNows.Add(new UserPlayingNow
                {
                    UserId = userId,
                    GameId = item.GameId,
                    StatusText = statusText,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new OnboardingStatusDto(true);
    }

    private static List<string> NormalizeList(IReadOnlyList<string>? values, string name)
    {
        if (values is null)
        {
            throw new InvalidOperationException($"{name} is required.");
        }

        if (values.Count > MaxListItems)
        {
            throw new InvalidOperationException($"{name} cannot contain more than {MaxListItems} items.");
        }

        if (values.Any(value => value is null))
        {
            throw new InvalidOperationException($"{name} cannot contain null values.");
        }

        if (values.Any(value => value.Trim().Length > MaxListItemLength))
        {
            throw new InvalidOperationException($"{name} items cannot be longer than {MaxListItemLength} characters.");
        }

        return values.Select(value => value.Trim()).Where(value => value.Length > 0).Distinct().ToList();
    }

    private static string? NormalizeOptionalText(string? value, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new InvalidOperationException($"Text cannot be longer than {maxLength} characters.");
        }

        return trimmed;
    }
}
