using Playr.Domain.Profiles;

namespace Playr.Api.Models.Lfg;

public sealed record CreateLfgGroupRequest(
    Guid GameId,
    int PlayersWanted,
    PlayStyle? PlayStyle,
    string? Note,
    int? PreferredMinAge = null,
    int? PreferredMaxAge = null,
    bool MicrophoneRequired = false);
