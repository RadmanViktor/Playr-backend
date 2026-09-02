using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Common;
using Playr.Api.Models.Games;
using Playr.Api.Models.Posts;
using Playr.Api.Models.Profiles;
using Playr.Application.Games;
using Playr.Application.Posts;
using Playr.Application.Profiles;
using Playr.Application.Common;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/profiles")]
public sealed class ProfilesController(
    IProfileService profileService, IPostService postService, IGameLibraryService gameLibraryService,
    IPlayingNowService playingNowService, IFavoriteGameService favoriteGameService) : ControllerBase
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
                    request.Genres ?? [],
                    request.ExternalLinks ?? new Dictionary<string, string>(),
                    request.TypicalPlayTimes ?? [],
                    request.DiscordUsername),
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
                new UpdateStatusCommand(
                    request.Status,
                    request.LookingForGameId,
                    request.LookingForPlayStyle,
                    request.LookingForGameNote,
                    request.LookingForPreferredMinAge,
                    request.LookingForPreferredMaxAge,
                    request.LookingForVoiceChatEnabled),
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

    [Authorize]
    [HttpPost("me/cover-image")]
    public async Task<ActionResult<ProfileResponse>> UploadCoverImage([FromForm] UploadCoverImageRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            var input = new FileUploadInput(request.CoverImage.OpenReadStream(), request.CoverImage.FileName, request.CoverImage.ContentType, request.CoverImage.Length);
            var profile = await profileService.UpdateCoverImageAsync(userId, baseUrl, input, cancellationToken);
            return Ok(ToResponse(profile));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPatch("me/cover-image-position")]
    public async Task<ActionResult<ProfileResponse>> UpdateCoverImagePosition(UpdateCoverImagePositionRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var profile = await profileService.UpdateCoverImagePositionAsync(userId, request.PositionX, request.PositionY, cancellationToken);
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
            p.AuthorActiveBadgeType, p.AuthorActiveBadgeLevel,
            p.GameId, p.GameName, p.GameCoverImageUrl, p.TextContent, p.Mood, p.Scope,
            p.Media.Select(m => new PostMediaResponse(m.Id, m.Url, m.MediaType, m.SortOrder)).ToList(),
            p.CreatedAt, p.LikesCount, p.LikedByCurrentUser, p.CommentsCount,
            p.Mentions.Select(m => new MentionResponse(m.UserId, m.Username, m.DisplayName)).ToList()
        )).ToList());
    }

    [HttpGet("{username}/library")]
    public async Task<ActionResult<IReadOnlyList<GameLibraryEntryResponse>>> GetLibraryByUsername(
        string username, CancellationToken cancellationToken)
    {
        var profile = await profileService.GetByUsernameAsync(username, null, cancellationToken);
        if (profile is null)
        {
            return NotFound(new { error = "Profile was not found." });
        }

        var entries = await gameLibraryService.GetLibraryAsync(profile.UserId, cancellationToken);
        return Ok(entries.Select(ToLibraryResponse).ToList());
    }

    [Authorize]
    [HttpPost("me/library")]
    public async Task<ActionResult<GameLibraryEntryResponse>> AddGameToLibrary(
        AddGameToLibraryRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var entry = await gameLibraryService.AddGameAsync(userId, request.GameId, cancellationToken);
            return Ok(ToLibraryResponse(entry));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("me/library/{gameId:guid}")]
    public async Task<ActionResult<GameLibraryEntryResponse>> RateGame(
        Guid gameId, RateGameRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var entry = await gameLibraryService.RateGameAsync(userId, gameId, request.Rating, request.ReviewText, cancellationToken);
            return Ok(ToLibraryResponse(entry));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("me/library/{gameId:guid}")]
    public async Task<IActionResult> RemoveGameFromLibrary(Guid gameId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            await gameLibraryService.RemoveGameAsync(userId, gameId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static GameLibraryEntryResponse ToLibraryResponse(GameLibraryEntryDto entry) => new(
        entry.GameId, entry.GameName, entry.GameCoverImageUrl, entry.Genre, entry.Rating, entry.ReviewText, entry.AddedAt, entry.UpdatedAt);

    [HttpGet("{username}/playing-now")]
    public async Task<ActionResult<IReadOnlyList<PlayingNowResponse>>> GetPlayingNowByUsername(
        string username, CancellationToken cancellationToken)
    {
        var profile = await profileService.GetByUsernameAsync(username, null, cancellationToken);
        if (profile is null)
        {
            return NotFound(new { error = "Profile was not found." });
        }

        var entries = await playingNowService.GetForUserAsync(profile.UserId, cancellationToken);
        return Ok(entries.Select(ToPlayingNowResponse).ToList());
    }

    [Authorize]
    [HttpPut("me/playing-now")]
    public async Task<ActionResult<PlayingNowResponse>> SetPlayingNow(
        SetPlayingNowRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var entry = await playingNowService.SetAsync(userId, request.GameId, request.StatusText, cancellationToken);
            return Ok(ToPlayingNowResponse(entry));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("me/playing-now/{gameId:guid}")]
    public async Task<IActionResult> RemovePlayingNow(Guid gameId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        await playingNowService.RemoveAsync(userId, gameId, cancellationToken);
        return NoContent();
    }

    private static PlayingNowResponse ToPlayingNowResponse(PlayingNowDto entry) => new(
        entry.GameId, entry.GameName, entry.GameCoverImageUrl, entry.StatusText, entry.CreatedAt, entry.UpdatedAt);

    [HttpGet("{username}/favorites")]
    public async Task<ActionResult<IReadOnlyList<FavoriteGameResponse>>> GetFavoritesByUsername(
        string username, CancellationToken cancellationToken)
    {
        var profile = await profileService.GetByUsernameAsync(username, null, cancellationToken);
        if (profile is null)
        {
            return NotFound(new { error = "Profile was not found." });
        }

        var entries = await favoriteGameService.GetForUserAsync(profile.UserId, cancellationToken);
        return Ok(entries.Select(ToFavoriteResponse).ToList());
    }

    [Authorize]
    [HttpPost("me/favorites")]
    public async Task<ActionResult<FavoriteGameResponse>> AddFavorite(
        AddFavoriteGameRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var entry = await favoriteGameService.AddAsync(userId, request.GameId, cancellationToken);
            return Ok(ToFavoriteResponse(entry));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("me/favorites/{gameId:guid}")]
    public async Task<IActionResult> RemoveFavorite(Guid gameId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            await favoriteGameService.RemoveAsync(userId, gameId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static FavoriteGameResponse ToFavoriteResponse(FavoriteGameDto entry) => new(
        entry.GameId, entry.GameName, entry.GameCoverImageUrl, entry.Genre, entry.CreatedAt);

    private static ProfileResponse ToResponse(ProfileDto profile) => new(
        profile.UserId,
        profile.Username,
        profile.DisplayName,
        profile.Bio,
        profile.AvatarUrl,
        profile.CoverImageUrl,
        profile.CoverImagePositionX,
        profile.CoverImagePositionY,
        profile.Region,
        profile.Languages,
        profile.Platforms,
        profile.Genres,
        profile.ExternalLinks,
        profile.Status,
        profile.LookingForGameId,
        profile.LookingForGameName,
        profile.LookingForPlayStyle,
        profile.LookingForGameNote,
        profile.TypicalPlayTimes,
        profile.HasCompletedOnboarding,
        profile.CreatedAt,
        profile.UpdatedAt,
        profile.ActiveBadgeType,
        profile.ActiveBadgeLevel,
        profile.RelationshipStatus?.ToString(),
        profile.PendingInvitationId,
        profile.DiscordUsername,
        profile.LookingForPreferredMinAge,
        profile.LookingForPreferredMaxAge,
        profile.LookingForVoiceChatEnabled);

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
            p.LookingForGameNote,
            p.RelationshipStatus.ToString(),
            p.PendingInvitationId,
            p.PreferredMinAge,
            p.PreferredMaxAge,
            p.VoiceChatEnabled)).ToList());
    }

}
