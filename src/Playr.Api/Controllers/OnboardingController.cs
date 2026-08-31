using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Onboarding;
using Playr.Application.Onboarding;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/onboarding")]
[Authorize]
public sealed class OnboardingController(IOnboardingService onboardingService) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<OnboardingStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var status = await onboardingService.GetStatusAsync(userId, cancellationToken);
        return Ok(new OnboardingStatusResponse(status.HasCompletedOnboarding));
    }

    [HttpPost("complete")]
    public async Task<ActionResult<OnboardingStatusResponse>> Complete(
        CompleteOnboardingRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var command = new CompleteOnboardingCommand(
                request.Platforms ?? [],
                request.Genres ?? [],
                request.GameIds ?? [],
                (request.PlayingNow ?? []).Select(p => new PlayingNowItem(p.GameId, p.StatusText)).ToList(),
                request.PlaystylePreference,
                request.UsuallyPlayingWith,
                request.TypicalPlayTimes ?? [],
                request.Bio);

            var status = await onboardingService.CompleteAsync(userId, command, cancellationToken);
            return Ok(new OnboardingStatusResponse(status.HasCompletedOnboarding));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
