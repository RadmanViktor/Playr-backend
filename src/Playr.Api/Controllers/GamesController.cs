using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Models.Games;
using Playr.Application.Games;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/games")]
public sealed class GamesController(IGameService gameService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GameResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var games = await gameService.GetAllAsync(cancellationToken);
        return Ok(games.Select(g => new GameResponse(g.Id, g.Name, g.CoverImageUrl, g.Genre)).ToList());
    }

    [HttpGet("search-external")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<ExternalGameSearchResponse>>> SearchExternal(
        [FromQuery] string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(Array.Empty<ExternalGameSearchResponse>());
        }

        var results = await gameService.SearchExternalAsync(query, cancellationToken);
        return Ok(results
            .Select(r => new ExternalGameSearchResponse(r.RawgId, r.Name, r.CoverImageUrl, r.Genre))
            .ToList());
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<GameResponse>> Create(
        [FromBody] CreateGameRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { error = "Name is required." });
        }

        var (game, created) = await gameService.CreateFromExternalAsync(
            new CreateGameCommand(request.RawgId, request.Name, request.CoverImageUrl, request.Genre),
            cancellationToken);

        var response = new GameResponse(game.Id, game.Name, game.CoverImageUrl, game.Genre);
        return created
            ? CreatedAtAction(nameof(GetAll), null, response)
            : Ok(response);
    }
}
