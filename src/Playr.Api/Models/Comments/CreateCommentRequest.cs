using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Comments;

public sealed record CreateCommentRequest(
    [Required][StringLength(500, MinimumLength = 1)] string TextContent,
    IReadOnlyList<Guid>? MentionedUserIds = null);
