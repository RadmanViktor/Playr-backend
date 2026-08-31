using Playr.Domain.Lfg;
using Playr.Domain.Profiles;

namespace Playr.Application.Lfg;

public sealed record LfgGroupDto(
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
    LfgGroupStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FilledAt,
    DateTimeOffset? CancelledAt,
    LfgMyMembershipStatus MyMembershipStatus,
    LfgApplicationStatus? MyApplicationStatus,
    LfgInviteStatus? MyInviteStatus);
