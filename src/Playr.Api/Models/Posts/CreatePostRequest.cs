using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Posts;

public sealed record CreatePostRequest(
    [Required] Guid GameId,
    [Required][StringLength(1000, MinimumLength = 1)] string TextContent,
    string? Mood);
