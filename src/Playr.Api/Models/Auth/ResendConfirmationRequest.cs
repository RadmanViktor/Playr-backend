using System.ComponentModel.DataAnnotations;

namespace Playr.Api.Models.Auth;

public sealed record ResendConfirmationRequest(
    [Required, EmailAddress] string Email);
