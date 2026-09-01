using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Auth;

public sealed record ForgotPasswordRequest(
    [Required, EmailAddress] string Email);
