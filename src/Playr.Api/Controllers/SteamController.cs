using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Playr.Api.Extensions;
using Playr.Api.Models.Steam;
using Playr.Api.Steam;
using Playr.Application.Steam;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/steam")]
public sealed class SteamController(
    ISteamService steamService,
    SteamLinkStateSigner stateSigner,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet("link")]
    [Authorize]
    public ActionResult<SteamLinkStartResponse> StartLink()
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var apiBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var state = stateSigner.Sign(userId);
        var returnUrl = $"{apiBaseUrl}/api/steam/callback?state={Uri.EscapeDataString(state)}";

        var redirectUrl = steamService.BuildLinkRedirectUrl(returnUrl, apiBaseUrl);
        return Ok(new SteamLinkStartResponse(redirectUrl));
    }

    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string state, CancellationToken cancellationToken)
    {
        var frontendBaseUrl = configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";

        if (!stateSigner.TryVerify(state, out var userId))
        {
            return Redirect($"{frontendBaseUrl}/settings?steam=error");
        }

        var query = Request.Query.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString());

        try
        {
            await steamService.CompleteLinkAsync(userId, query, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return Redirect($"{frontendBaseUrl}/settings?steam=error");
        }

        return Redirect($"{frontendBaseUrl}/settings?steam=linked");
    }

    [HttpDelete("link")]
    [Authorize]
    public async Task<IActionResult> Unlink(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        await steamService.UnlinkAsync(userId, cancellationToken);
        return NoContent();
    }

    [HttpGet("status")]
    [Authorize]
    public async Task<ActionResult<SteamAccountResponse?>> GetStatus(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var account = await steamService.GetAccountAsync(userId, cancellationToken);
        return Ok(account is null ? null : ToResponse(account));
    }

    [HttpGet("games/{userId:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<SteamGameResponse>>> GetGames(Guid userId, CancellationToken cancellationToken)
    {
        var games = await steamService.GetGamesAsync(userId, cancellationToken);
        return Ok(games.Select(g => new SteamGameResponse(g.AppId, g.Name, g.IconUrl, g.PlaytimeForeverMinutes, g.PlaytimeRecentMinutes)).ToList());
    }

    [HttpGet("games/{userId:guid}/{appId:long}/achievements")]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<SteamAchievementResponse>>> GetAchievements(Guid userId, long appId, CancellationToken cancellationToken)
    {
        var achievements = await steamService.GetAchievementsAsync(userId, appId, cancellationToken);
        return Ok(achievements.Select(a => new SteamAchievementResponse(a.ApiName, a.DisplayName, a.IconUrl, a.IconGrayUrl, a.Achieved, a.UnlockedAt)).ToList());
    }

    private static SteamAccountResponse ToResponse(SteamAccountDto account) => new(
        account.UserId,
        account.SteamId,
        account.DisplayName,
        account.AvatarUrl,
        account.IsPublic,
        account.LinkedAt,
        account.LastSyncedAt);
}
