namespace Playr.Domain.Games;

/// <summary>
/// A game a user has added to their personal library. Doubles as the record used
/// to rate/review that game - a rating can only be set for games already in the library.
/// </summary>
public sealed class UserGameLibraryEntry
{
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;

    /// <summary>1-5 star rating, or null if the game has been added but not yet rated.</summary>
    public int? Rating { get; set; }
    public string? ReviewText { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
