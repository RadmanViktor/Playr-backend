using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Auth;

public sealed record LoginRequest(
    [Required] string UsernameOrEmail,
    [Required] string Password);
