using Playr.Domain.Profiles;

namespace Playr.Application.Lfg;

public sealed record CreateLfgGroupCommand(Guid GameId, int PlayersWanted, PlayStyle? PlayStyle, string? Note);
