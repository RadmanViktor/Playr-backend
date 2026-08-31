using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Playr.Api.Extensions;
using Playr.Api.Models.Badges;
using Playr.Application.Badges;
using Playr.Domain.Badges;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/badges")]
[Authorize]
public sealed class BadgesController(IBadgeService badgeService, IOptions<AdminOptions> adminOptions) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<UserBadgesResponse>> GetMine(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var badges = await badgeService.GetBadgesAsync(userId, cancellationToken);
        return Ok(ToResponse(badges));
    }

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<UserBadgesResponse>> GetForUser(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var badges = await badgeService.GetBadgesAsync(userId, cancellationToken);
            return Ok(ToResponse(badges));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("active")]
    public async Task<ActionResult> SetActive(SetActiveBadgeRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        BadgeType? badgeType = null;
        if (!string.IsNullOrWhiteSpace(request.BadgeType))
        {
            if (!Enum.TryParse<BadgeType>(request.BadgeType, ignoreCase: true, out var parsed))
            {
                return BadRequest(new { error = "Invalid badge type." });
            }
            badgeType = parsed;
        }

        try
        {
            await badgeService.SetActiveBadgeAsync(userId, badgeType, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static UserBadgesResponse ToResponse(UserBadgesDto dto) => new(
        dto.UserId,
        dto.Badges.Select(b => new BadgeResponse(b.Type, b.Level, b.UnlockedAt)).ToList(),
        dto.ActiveBadgeType,
        dto.ActiveBadgeLevel);

    /// <summary>
    /// Admin-only: manually grants a badge to a user, bypassing stat thresholds.
    /// Used for non-stat-based badges such as "Creator". Requires a valid logged-in
    /// user (via [Authorize]) plus the correct X-Admin-Secret header - configure
    /// Admin:GrantSecret to enable; the endpoint always returns 404 if that secret
    /// is not configured, so it's fully disabled by default.
    /// </summary>
    [HttpPost("grant")]
    public async Task<ActionResult> Grant(GrantBadgeRequest request, CancellationToken cancellationToken)
    {
        var configuredSecret = adminOptions.Value.GrantSecret;
        if (string.IsNullOrEmpty(configuredSecret))
        {
            return NotFound();
        }

        if (!Request.Headers.TryGetValue("X-Admin-Secret", out var providedSecret) ||
            !FixedTimeEquals(providedSecret.ToString(), configuredSecret))
        {
            return NotFound();
        }

        if (!Enum.TryParse<BadgeType>(request.BadgeType, ignoreCase: true, out var type))
        {
            return BadRequest(new { error = "Invalid badge type." });
        }

        var level = BadgeLevel.Gold;
        if (!string.IsNullOrWhiteSpace(request.Level) && !Enum.TryParse(request.Level, ignoreCase: true, out level))
        {
            return BadRequest(new { error = "Invalid badge level." });
        }

        try
        {
            await badgeService.GrantBadgeAsync(request.UserId, type, level, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Constant-time string comparison to avoid leaking the secret via timing.</summary>
    private static bool FixedTimeEquals(string a, string b)
    {
        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }
}
