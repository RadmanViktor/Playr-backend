using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Posts;

public sealed class UpdatePostRequest
{
    [Required]
    [StringLength(1000, MinimumLength = 1)]
    public string TextContent { get; set; } = string.Empty;

    public string? Mood { get; set; }

    public List<IFormFile> Media { get; set; } = [];

    public List<Guid> RemoveMediaIds { get; set; } = [];
}
