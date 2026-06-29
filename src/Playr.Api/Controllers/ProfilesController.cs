using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Profiles;
using Playr.Application.Profiles;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/profiles")]
public sealed class ProfilesController(IProfileService profileService) : ControllerBase
{
    [HttpGet("{username}")]
    public async Task<ActionResult<ProfileResponse>> GetByUsername(string username, CancellationToken cancellationToken)
    {
        var profile = await profileService.GetByUsernameAsync(username, cancellationToken);
        return profile is null ? NotFound(new { error = "Profile was not found." }) : Ok(ToResponse(profile));
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<ProfileResponse>> UpdateMe(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var profile = await profileService.UpdateCurrentUserAsync(
                User.GetUserId(),
                new UpdateProfileCommand(
                    request.DisplayName,
                    request.Bio,
                    request.AvatarUrl,
                    request.Region,
                    request.Languages ?? [],
                    request.Platforms ?? [],
                    request.ExternalLinks ?? new Dictionary<string, string>(),
                    request.CurrentlyPlayingGames ?? [],
                    request.LookingForPlayers),
                cancellationToken);

            return Ok(ToResponse(profile));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static ProfileResponse ToResponse(ProfileDto profile) => new(
        profile.UserId,
        profile.Username,
        profile.DisplayName,
        profile.Bio,
        profile.AvatarUrl,
        profile.Region,
        profile.Languages,
        profile.Platforms,
        profile.ExternalLinks,
        profile.CurrentlyPlayingGames,
        profile.LookingForPlayers,
        profile.CreatedAt,
        profile.UpdatedAt);
}
