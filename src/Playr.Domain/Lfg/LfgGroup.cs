using Playr.Domain.Games;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;

namespace Playr.Domain.Lfg;

public sealed class LfgGroup
{
    public Guid Id { get; set; }
    public Guid CreatorUserId { get; set; }
    public ApplicationUser Creator { get; set; } = null!;
    public Guid GameId { get; set; }
    public Game Game { get; set; } = null!;
    public PlayStyle? PlayStyle { get; set; }
    public string? Note { get; set; }
    public int? PreferredMinAge { get; set; }
    public int? PreferredMaxAge { get; set; }
    public bool MicrophoneRequired { get; set; }
    public int PlayersWanted { get; set; }
    public LfgGroupStatus Status { get; set; } = LfgGroupStatus.Open;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FilledAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public ICollection<LfgGroupMember> Members { get; set; } = [];
    public ICollection<LfgGroupApplication> Applications { get; set; } = [];
    public ICollection<LfgGroupInvite> Invites { get; set; } = [];
}
