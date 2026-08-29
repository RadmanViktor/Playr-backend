namespace Playr.Application.Auth;

public sealed record AuthUserDto(Guid Id, string Email, string Username, string DisplayName, bool EmailConfirmed);

public sealed record AuthResult(string AccessToken, DateTimeOffset ExpiresAt);
