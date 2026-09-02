using Playr.Domain.Badges;
using Playr.Domain.Games;
using Playr.Domain.Identity;

namespace Playr.Domain.Profiles;

public sealed class UserProfile
{
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public double CoverImagePositionX { get; set; } = 50;
    public double CoverImagePositionY { get; set; } = 50;
    public string? Region { get; set; }
    public string? DiscordUsername { get; set; }
    public List<string> Languages { get; set; } = [];
    public List<string> Platforms { get; set; } = [];
    public List<string> Genres { get; set; } = [];
    public Dictionary<string, string> ExternalLinks { get; set; } = [];
    public ProfileStatus Status { get; set; } = ProfileStatus.Online;
    public Guid? LookingForGameId { get; set; }
    public Game? LookingForGame { get; set; }
    public PlayStyle? LookingForPlayStyle { get; set; }
    public string? LookingForGameNote { get; set; }
    public int? LookingForPreferredMinAge { get; set; }
    public int? LookingForPreferredMaxAge { get; set; }
    public bool LookingForVoiceChatEnabled { get; set; }
    public List<string> TypicalPlayTimes { get; set; } = [];
    public bool HasCompletedOnboarding { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool ChatSoundEnabled { get; set; } = true;
    public bool ChatBrowserNotificationsEnabled { get; set; } = true;
    public BadgeType? ActiveBadgeType { get; set; }
    public BadgeLevel? ActiveBadgeLevel { get; set; }
}
