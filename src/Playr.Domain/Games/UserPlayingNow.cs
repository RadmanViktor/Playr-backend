namespace Playr.Domain.Games;

/// <summary>
/// A game a user has marked as "playing now" on their profile, with an optional short status.
/// Distinct from <see cref="UserGameLibraryEntry"/> which tracks the user's full library/ratings.
/// </summary>
public sealed class UserPlayingNow
{
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;

    /// <summary>Optional short free-text status, e.g. "Grinding Premier with friends".</summary>
    public string? StatusText { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
