using Playr.Domain.Profiles;

namespace Playr.Application.Profiles;

public sealed record UpdateStatusCommand(
    ProfileStatus Status,
    Guid? LookingForGameId,
    PlayStyle? LookingForPlayStyle);
