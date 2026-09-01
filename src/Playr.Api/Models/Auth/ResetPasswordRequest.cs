using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Auth;

public sealed record ResetPasswordRequest(
    [Required] Guid UserId,
    [Required] string Token,
    [Required] string NewPassword);
