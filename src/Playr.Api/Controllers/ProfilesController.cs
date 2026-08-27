using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Posts;
using Playr.Api.Models.Profiles;
using Playr.Application.Posts;
using Playr.Application.Profiles;
using Playr.Application.Common;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/profiles")]
public sealed class ProfilesController(IProfileService profileService, IPostService postService) : ControllerBase
{
    [HttpGet("{username}")]
    public async Task<ActionResult<ProfileResponse>> GetByUsername(string username, CancellationToken cancellationToken)
    {
        Guid? currentUserId = User.TryGetUserId(out var uid) ? uid : null;
        var profile = await profileService.GetByUsernameAsync(username, currentUserId, cancellationToken);
        return profile is null ? NotFound(new { error = "Profile was not found." }) : Ok(ToResponse(profile));
    }

    [Authorize]
    [HttpPut("me")]
    public async Task<ActionResult<ProfileResponse>> UpdateMe(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var profile = await profileService.UpdateCurrentUserAsync(
                userId,
                new UpdateProfileCommand(
                    request.DisplayName,
                    request.Bio,
                    request.Region,
                    request.Languages ?? [],
                    request.Platforms ?? [],
                    request.ExternalLinks ?? new Dictionary<string, string>(),
                    request.CurrentlyPlayingGames ?? []),
                cancellationToken);

            return Ok(ToResponse(profile));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPatch("me/status")]
    public async Task<ActionResult<ProfileResponse>> UpdateStatus(UpdateStatusRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var profile = await profileService.UpdateStatusAsync(
                userId,
                new UpdateStatusCommand(request.Status, request.LookingForGameId, request.LookingForPlayStyle),
                cancellationToken);

            return Ok(ToResponse(profile));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("me/avatar")]
    public async Task<ActionResult<ProfileResponse>> UploadAvatar([FromForm] UploadAvatarRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var input = new FileUploadInput(request.Avatar.OpenReadStream(), request.Avatar.FileName, request.Avatar.ContentType, request.Avatar.Length);
            var profile = await profileService.UpdateAvatarAsync(userId, baseUrl, input, cancellationToken);
            return Ok(ToResponse(profile));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{username}/posts")]
    public async Task<ActionResult<IReadOnlyList<PostResponse>>> GetPostsByUsername(
        string username, CancellationToken cancellationToken)
    {
        Guid? currentUserId = User.TryGetUserId(out var uid) ? uid : null;
        var posts = await postService.GetByUsernameAsync(username, currentUserId, cancellationToken);
        return Ok(posts.Select(p => new PostResponse(
            p.Id, p.AuthorId, p.AuthorUsername, p.AuthorDisplayName, p.AuthorAvatarUrl,
            p.GameId, p.GameName, p.GameCoverImageUrl, p.TextContent, p.Mood,
            p.Media.Select(m => new PostMediaResponse(m.Id, m.Url, m.MediaType, m.SortOrder)).ToList(),
            p.CreatedAt, p.LikesCount, p.LikedByCurrentUser, p.CommentsCount
        )).ToList());
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
        profile.Status,
        profile.LookingForGameId,
        profile.LookingForGameName,
        profile.LookingForPlayStyle,
        profile.CreatedAt,
        profile.UpdatedAt,
        profile.RelationshipStatus?.ToString(),
        profile.PendingInvitationId);

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<ProfileSearchResponse>>> Search(
        [FromQuery] string? q, CancellationToken cancellationToken)
    {
        var results = await profileService.SearchAsync(q ?? string.Empty, cancellationToken);
        return Ok(results.Select(r => new ProfileSearchResponse(r.UserId, r.Username, r.DisplayName, r.AvatarUrl)).ToList());
    }

    [Authorize]
    [HttpGet("looking-for-game")]
    public async Task<ActionResult<IReadOnlyList<LookingForGamePlayerResponse>>> GetLookingForGamePlayers(
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var players = await profileService.GetLookingForGamePlayersAsync(userId, cancellationToken);
        return Ok(players.Select(p => new LookingForGamePlayerResponse(
            p.UserId,
            p.Username,
            p.DisplayName,
            p.AvatarUrl,
            p.LookingForGameId,
            p.LookingForGameName,
            p.LookingForPlayStyle,
            p.RelationshipStatus.ToString(),
            p.PendingInvitationId)).ToList());
    }
}
