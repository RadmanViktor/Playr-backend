namespace Playr.Domain.Games;

public sealed class Game
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? Genre { get; set; }

    /// <summary>
    /// The RAWG.io game id this catalog entry was created from, used to avoid creating
    /// duplicate entries when a user adds a game that already exists in the catalog.
    /// Null for games that were seeded manually (without a RAWG match).
    /// </summary>
    public long? RawgId { get; set; }
}
