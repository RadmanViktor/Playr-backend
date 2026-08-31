using Playr.Domain.Lfg;

namespace Playr.Application.Lfg;

public sealed record LfgGroupApplicationDto(
    Guid Id,
    Guid LfgGroupId,
    string GameName,
    Guid ApplicantUserId,
    string ApplicantUsername,
    string ApplicantDisplayName,
    string? ApplicantAvatarUrl,
    LfgApplicationStatus Status,
    string? Message,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);
