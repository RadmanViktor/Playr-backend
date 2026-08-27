namespace Playr.Domain.Steam;

/// <summary>
/// A cached copy of a game owned by a user on Steam, synced periodically from the Steam Web API.
/// </summary>
public sealed class SteamOwnedGame
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public long AppId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public int PlaytimeForeverMinutes { get; set; }
    public int PlaytimeRecentMinutes { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; } = DateTimeOffset.UtcNow;
}
