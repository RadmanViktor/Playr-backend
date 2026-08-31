using Playr.Domain.Identity;

namespace Playr.Domain.Lfg;

public sealed class LfgGroupApplication
{
    public Guid Id { get; set; }
    public Guid LfgGroupId { get; set; }
    public LfgGroup LfgGroup { get; set; } = null!;
    public Guid ApplicantUserId { get; set; }
    public ApplicationUser Applicant { get; set; } = null!;
    public LfgApplicationStatus Status { get; set; } = LfgApplicationStatus.Pending;
    public string? Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RespondedAt { get; set; }
}
