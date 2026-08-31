using Playr.Domain.Profiles;

namespace Playr.Application.Onboarding;

public sealed record PlayingNowItem(Guid GameId, string? StatusText);

public sealed record CompleteOnboardingCommand(
    IReadOnlyList<string> Platforms,
    IReadOnlyList<string> Genres,
    IReadOnlyList<Guid> GameIds,
    IReadOnlyList<PlayingNowItem> PlayingNow,
    PlaystylePreference? PlaystylePreference,
    UsuallyPlayingWith? UsuallyPlayingWith,
    IReadOnlyList<string> TypicalPlayTimes,
    string? Bio);

public sealed record OnboardingStatusDto(bool HasCompletedOnboarding);

public interface IOnboardingService
{
    Task<OnboardingStatusDto> GetStatusAsync(Guid userId, CancellationToken cancellationToken);

    Task<OnboardingStatusDto> CompleteAsync(Guid userId, CompleteOnboardingCommand command, CancellationToken cancellationToken);
}
