using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Comments;

public sealed record UpdateCommentRequest(
    [Required][StringLength(500, MinimumLength = 1)] string TextContent);
