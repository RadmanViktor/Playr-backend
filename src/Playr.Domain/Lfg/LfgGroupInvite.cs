using Playr.Domain.Identity;

namespace Playr.Domain.Lfg;

public sealed class LfgGroupInvite
{
    public Guid Id { get; set; }
    public Guid LfgGroupId { get; set; }
    public LfgGroup LfgGroup { get; set; } = null!;
    public Guid InviterUserId { get; set; }
    public ApplicationUser Inviter { get; set; } = null!;
    public Guid InviteeUserId { get; set; }
    public ApplicationUser Invitee { get; set; } = null!;
    public LfgInviteStatus Status { get; set; } = LfgInviteStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RespondedAt { get; set; }
}
