using Playr.Domain.Badges;

namespace Playr.Application.Badges;

/// <summary>
/// Hardcoded stat thresholds required to reach each <see cref="BadgeLevel"/> for a given
/// <see cref="BadgeType"/>. <see cref="BadgeType.FirstHundredUsers"/> is not threshold-based
/// (it's a one-time signup-rank check) and is intentionally not represented here.
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
