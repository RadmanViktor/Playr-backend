namespace Playr.Domain.Games;

public sealed class Game
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CoverImageUrl { get; set; }
    public string? Genre { get; set; }
}
