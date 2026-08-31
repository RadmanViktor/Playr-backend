using Playr.Domain.Lfg;

namespace Playr.Application.Lfg;

public sealed record LfgGroupInviteDto(
    Guid Id,
    Guid LfgGroupId,
    string GameName,
    Guid InviterUserId,
    Guid InviteeUserId,
    string InviteeUsername,
    string InviteeDisplayName,
    string? InviteeAvatarUrl,
    LfgInviteStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);
