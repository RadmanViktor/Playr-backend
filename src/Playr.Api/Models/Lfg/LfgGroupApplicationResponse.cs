namespace Playr.Api.Models.Lfg;

public sealed record LfgGroupApplicationResponse(
    Guid Id,
    Guid LfgGroupId,
    string GameName,
    Guid ApplicantUserId,
    string ApplicantUsername,
    string ApplicantDisplayName,
    string? ApplicantAvatarUrl,
    string Status,
    string? Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);
