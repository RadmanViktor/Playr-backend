namespace Playr.Application.Posts;

public sealed record UpdatePostCommand(
    string TextContent,
    string? Mood,
    IReadOnlyList<PostMediaInput> NewMedia,
    IReadOnlyList<Guid> RemoveMediaIds);
