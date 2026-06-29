namespace Playr.Api.Models.Auth;

public sealed record UserResponse(Guid Id, string Email, string Username, string DisplayName);
