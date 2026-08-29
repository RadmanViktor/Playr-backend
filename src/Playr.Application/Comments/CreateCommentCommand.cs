namespace Playr.Application.Comments;

public sealed record CreateCommentCommand(string TextContent, IReadOnlyList<Guid>? MentionedUserIds = null);
