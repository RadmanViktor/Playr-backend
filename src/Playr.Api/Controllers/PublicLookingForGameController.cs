using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Models.Profiles;
using Playr.Application.Profiles;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/profiles/looking-for-game/public")]
public sealed class PublicLookingForGameController(
    IPublicLookingForGameService publicLookingForGameService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<PublicLookingForGameSummaryResponse>> Get(
        CancellationToken cancellationToken)
    {
        var summary = await publicLookingForGameService.GetSummaryAsync(cancellationToken);
        return Ok(new PublicLookingForGameSummaryResponse(
            summary.TotalCount,
            summary.FeaturedGame is null
                ? null
                : new PublicLookingForGameFeaturedGameResponse(
                    summary.FeaturedGame.Name,
                    summary.FeaturedGame.CoverImageUrl,
                    summary.FeaturedGame.PlayerCount),
            summary.Players.Select(player => new PublicLookingForGamePlayerResponse(
                player.Username,
                player.DisplayName,
                player.AvatarUrl,
                player.GameName,
                player.PlayStyle)).ToList()));
    }
}
