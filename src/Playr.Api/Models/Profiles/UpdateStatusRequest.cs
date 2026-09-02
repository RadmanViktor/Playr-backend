using System.ComponentModel.DataAnnotations;
using Playr.Domain.Profiles;

namespace Playr.Api.Models.Profiles;

public sealed record UpdateStatusRequest(
    ProfileStatus Status,
    Guid? LookingForGameId,
    PlayStyle? LookingForPlayStyle,
    [StringLength(200)] string? LookingForGameNote,
    int? LookingForPreferredMinAge = null,
    int? LookingForPreferredMaxAge = null,
    bool LookingForVoiceChatEnabled = false);
