namespace Playr.Application.Posts;

public sealed record UpdatePostCommand(
    string TextContent,
    string? Mood,
    PostMediaInput? Media,
    bool RemoveMedia);
