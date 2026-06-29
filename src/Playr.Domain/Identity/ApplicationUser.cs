using Microsoft.AspNetCore.Identity;
using Playr.Domain.Profiles;

namespace Playr.Domain.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public UserProfile? Profile { get; set; }
}
