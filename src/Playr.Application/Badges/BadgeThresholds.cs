using Playr.Domain.Badges;

namespace Playr.Application.Badges;

/// <summary>
/// Hardcoded stat thresholds required to reach each <see cref="BadgeLevel"/> for a given
/// <see cref="BadgeType"/>. <see cref="BadgeType.FirstHundredUsers"/>, <see cref="BadgeType.Trailblazer"/>,
/// <see cref="BadgeType.NightOwl"/> and <see cref="BadgeType.Veteran"/> are not threshold-based
/// (one-time checks, always Gold) and are intentionally not represented here, nor are
/// <see cref="BadgeType.Creator"/> and <see cref="BadgeType.Admin"/> (manually granted only).
/// </summary>
public static class BadgeThresholds
{
    private static readonly IReadOnlyDictionary<BadgeType, IReadOnlyDictionary<BadgeLevel, int>> Thresholds =
        new Dictionary<BadgeType, IReadOnlyDictionary<BadgeLevel, int>>
        {
            [BadgeType.Poster] = new Dictionary<BadgeLevel, int>
            {
                [BadgeLevel.Bronze] = 25,
                [BadgeLevel.Silver] = 100,
                [BadgeLevel.Gold] = 250,
            },
            [BadgeType.GameCritic] = new Dictionary<BadgeLevel, int>
            {
                [BadgeLevel.Bronze] = 5,
                [BadgeLevel.Silver] = 15,
                [BadgeLevel.Gold] = 50,
            },
            [BadgeType.Commentator] = new Dictionary<BadgeLevel, int>
            {
                [BadgeLevel.Bronze] = 50,
                [BadgeLevel.Silver] = 200,
                [BadgeLevel.Gold] = 500,
            },
            [BadgeType.Inviter] = new Dictionary<BadgeLevel, int>
            {
                [BadgeLevel.Bronze] = 3,
                [BadgeLevel.Silver] = 10,
                [BadgeLevel.Gold] = 25,
            },
            [BadgeType.Supporter] = new Dictionary<BadgeLevel, int>
            {
                [BadgeLevel.Bronze] = 25,
                [BadgeLevel.Silver] = 100,
                [BadgeLevel.Gold] = 300,
            },
            [BadgeType.Popular] = new Dictionary<BadgeLevel, int>
            {
                [BadgeLevel.Bronze] = 15,
                [BadgeLevel.Silver] = 50,
                [BadgeLevel.Gold] = 150,
            },
            [BadgeType.Socialite] = new Dictionary<BadgeLevel, int>
            {
                [BadgeLevel.Bronze] = 3,
                [BadgeLevel.Silver] = 10,
                [BadgeLevel.Gold] = 25,
            },
            [BadgeType.Chatterbox] = new Dictionary<BadgeLevel, int>
            {
                [BadgeLevel.Bronze] = 50,
                [BadgeLevel.Silver] = 200,
                [BadgeLevel.Gold] = 500,
            },
            [BadgeType.Collector] = new Dictionary<BadgeLevel, int>
            {
                [BadgeLevel.Bronze] = 10,
                [BadgeLevel.Silver] = 30,
                [BadgeLevel.Gold] = 75,
            },
            [BadgeType.Reactor] = new Dictionary<BadgeLevel, int>
            {
                [BadgeLevel.Bronze] = 25,
                [BadgeLevel.Silver] = 100,
                [BadgeLevel.Gold] = 300,
            },
        };

    /// <summary>The number of first-registered users who qualify for <see cref="BadgeType.FirstHundredUsers"/>.</summary>
    public const int FirstHundredUsersCount = 100;

    /// <summary>
    /// Returns the highest <see cref="BadgeLevel"/> reached for <paramref name="type"/> given
    /// <paramref name="statValue"/>, or <see cref="BadgeLevel.None"/> if no tier was reached.
    /// </summary>
    public static BadgeLevel GetLevelForStat(BadgeType type, int statValue)
    {
        if (!Thresholds.TryGetValue(type, out var levels))
        {
            return BadgeLevel.None;
        }

        var level = BadgeLevel.None;
        foreach (var (candidateLevel, threshold) in levels.OrderBy(kvp => kvp.Value))
        {
            if (statValue >= threshold)
            {
                level = candidateLevel;
            }
        }

        return level;
    }
}
