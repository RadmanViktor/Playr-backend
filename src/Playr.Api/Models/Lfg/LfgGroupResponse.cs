using Playr.Domain.Profiles;

namespace Playr.Api.Models.Lfg;

public sealed record LfgGroupResponse(
    Guid Id,
    Guid CreatorUserId,
    string CreatorUsername,
    string CreatorDisplayName,
    string? CreatorAvatarUrl,
    Guid GameId,
    string GameName,
    string? GameCoverImageUrl,
    PlayStyle? PlayStyle,
    string? Note,
    int PlayersWanted,
    int AcceptedCount,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FilledAt,
    DateTimeOffset? CancelledAt,
    string MyMembershipStatus,
    string? MyApplicationStatus,
    string? MyInviteStatus);
