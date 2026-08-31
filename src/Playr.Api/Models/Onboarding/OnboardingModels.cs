namespace Playr.Api.Models.Onboarding;

public sealed record PlayingNowItemRequest(Guid GameId, string? StatusText);

public sealed record CompleteOnboardingRequest(
    IReadOnlyList<string>? Platforms,
    IReadOnlyList<string>? Genres,
    IReadOnlyList<Guid>? GameIds,
    IReadOnlyList<PlayingNowItemRequest>? PlayingNow,
    IReadOnlyList<string>? TypicalPlayTimes,
    string? Bio);

public sealed record OnboardingStatusResponse(bool HasCompletedOnboarding);
