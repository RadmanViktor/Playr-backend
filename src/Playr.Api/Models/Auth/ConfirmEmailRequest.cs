using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Auth;

public sealed record ConfirmEmailRequest(
    [Required] Guid UserId,
    [Required] string Token);
