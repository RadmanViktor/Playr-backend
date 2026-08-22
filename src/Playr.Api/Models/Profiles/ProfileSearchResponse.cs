namespace Playr.Api.Models.Profiles;

public sealed record ProfileSearchResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);
