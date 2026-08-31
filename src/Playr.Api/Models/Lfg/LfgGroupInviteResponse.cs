namespace Playr.Api.Models.Lfg;

public sealed record LfgGroupInviteResponse(
    Guid Id,
    Guid LfgGroupId,
    string GameName,
    Guid InviterUserId,
    Guid InviteeUserId,
    string InviteeUsername,
    string InviteeDisplayName,
    string? InviteeAvatarUrl,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? RespondedAt);
