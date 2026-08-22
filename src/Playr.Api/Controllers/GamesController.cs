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
}
