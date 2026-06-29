namespace Playr.Api.Models.Auth;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);
