using Microsoft.EntityFrameworkCore;
using Playr.Application.Profiles;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Profiles;

public sealed class PublicLookingForGameService(PlayrDbContext dbContext) : IPublicLookingForGameService
{
    public async Task<PublicLookingForGameSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var seekers = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.Status == ProfileStatus.LookingForGame
                && profile.LookingForGameId != null
                && profile.LookingForPlayStyle != null)
            .Select(profile => new
            {
                profile.Username,
                profile.DisplayName,
                profile.AvatarUrl,
                GameId = profile.LookingForGameId!.Value,
                GameName = profile.LookingForGame!.Name,
                GameCoverImageUrl = profile.LookingForGame.CoverImageUrl,
                PlayStyle = profile.LookingForPlayStyle!.Value,
                profile.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        var featuredGame = seekers
            .GroupBy(seeker => new { seeker.GameId, seeker.GameName, seeker.GameCoverImageUrl })
            .Select(group => new
            {
                group.Key.GameId,
                group.Key.GameName,
                group.Key.GameCoverImageUrl,
                PlayerCount = group.Count(),
            })
            .OrderByDescending(game => game.PlayerCount)
            .ThenBy(game => game.GameName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(game => game.GameId)
            .Select(game => new PublicLookingForGameFeaturedGameDto(
                game.GameName,
                game.GameCoverImageUrl,
                game.PlayerCount))
            .FirstOrDefault();

        var players = seekers
            .OrderByDescending(seeker => seeker.UpdatedAt)
            .ThenBy(seeker => seeker.Username, StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .Select(seeker => new PublicLookingForGamePlayerDto(
                seeker.Username,
                seeker.DisplayName,
                seeker.AvatarUrl,
                seeker.GameName,
                seeker.PlayStyle))
            .ToList();

        return new PublicLookingForGameSummaryDto(seekers.Count, featuredGame, players);
    }
}
