namespace Playr.Application.Posts;

public sealed record CreatePostCommand(
    Guid GameId,
    string TextContent,
    string? Mood,
    PostMediaInput? Media);
