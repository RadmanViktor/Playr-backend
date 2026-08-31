using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Playr.Api.Models.Profiles;

public sealed class UploadCoverImageRequest
{
    [Required]
    public IFormFile CoverImage { get; set; } = null!;
}
