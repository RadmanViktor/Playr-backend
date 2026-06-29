using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Auth;

public sealed record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, StringLength(32, MinimumLength = 3)] string Username,
    [Required, MinLength(8)] string Password);
