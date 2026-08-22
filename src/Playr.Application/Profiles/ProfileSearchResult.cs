namespace Playr.Application.Profiles;

public sealed record ProfileSearchResult(
    Guid UserId,
    string Username,
    string DisplayName,
    string? AvatarUrl);
