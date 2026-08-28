using System.ComponentModel.DataAnnotations;
using Playr.Domain.Profiles;

namespace Playr.Api.Models.Profiles;

public sealed record UpdateStatusRequest(
    ProfileStatus Status,
    Guid? LookingForGameId,
    PlayStyle? LookingForPlayStyle,
    [property: StringLength(200)] string? LookingForGameNote);
