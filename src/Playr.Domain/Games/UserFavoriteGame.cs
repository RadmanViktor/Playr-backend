namespace Playr.Domain.Games;

/// <summary>
/// A game a user has marked as a favorite on their profile. Distinct from
/// <see cref="UserGameLibraryEntry"/> (full library/ratings) and <see cref="UserPlayingNow"/>
/// (currently playing). Favorites are shown prominently on the profile Overview.
/// </summary>
public sealed class UserFavoriteGame
{
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }
}
