using Playr.Domain.Identity;

namespace Playr.Domain.Lfg;

public sealed class LfgGroupMember
{
    public Guid LfgGroupId { get; set; }
    public LfgGroup LfgGroup { get; set; } = null!;
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsCreator { get; set; }
}
