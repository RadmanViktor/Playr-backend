using Playr.Domain.Profiles;

namespace Playr.Api.Models.Profiles;

public sealed record UpdateStatusRequest(
    ProfileStatus Status,
    Guid? LookingForGameId,
    PlayStyle? LookingForPlayStyle);
