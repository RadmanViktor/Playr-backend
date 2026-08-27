namespace Playr.Domain.Steam;

/// <summary>
/// A cached achievement unlock state for a user's owned game. Structurally prepared now;
/// population/sync is implemented in a later milestone (not part of the initial MVP sync).
/// </summary>
public sealed class SteamAchievement
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public long AppId { get; set; }
    public string ApiName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? IconUrl { get; set; }
    public string? IconGrayUrl { get; set; }
    public bool Achieved { get; set; }
    public DateTimeOffset? UnlockedAt { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; } = DateTimeOffset.UtcNow;
}
