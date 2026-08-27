using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Playr.Api.Models.Profiles;

public sealed class UploadAvatarRequest
{
    [Required]
    public IFormFile Avatar { get; set; } = null!;
}
