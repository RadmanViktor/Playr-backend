namespace Playr.Application.Posts;

public sealed record CreatePostCommand(
    Guid GameId,
    string TextContent,
    string? Mood,
    IReadOnlyList<PostMediaInput> Media,
    IReadOnlyList<Guid>? MentionedUserIds = null,
    string? Scope = null);
