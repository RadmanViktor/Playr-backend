using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Playr.Application.Games;
using Playr.Domain.Games;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Games;
using Playr.Infrastructure.Rawg;

namespace Playr.Application.Tests.Games;

public sealed class GameServiceTests : IAsyncDisposable
{
    private static readonly Guid HollowKnightGameId = new("00000001-0000-0000-0000-000000000007");
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly GameService _service;

    public GameServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();
        // EnsureCreated seeds 8 games via HasData; no additional rows needed
        var rawgApiClient = new RawgApiClient(
            new HttpClient { BaseAddress = new Uri("https://api.rawg.io") },
            Options.Create(new RawgOptions()),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<RawgApiClient>.Instance);
        _service = new GameService(_dbContext, rawgApiClient);
    }

    [Fact]
    public async Task GetAllAsync_returns_all_games_ordered_by_name()
    {
        var result = await _service.GetAllAsync(CancellationToken.None);

        // The seed contains 8 games; verify order is ascending by name
        result.Should().NotBeEmpty();
        result.Select(g => g.Name).Should().BeInAscendingOrder();
        // First seeded game alphabetically is "Apex Legends"
        result[0].Name.Should().Be("Apex Legends");
    }

    [Fact]
    public async Task GetAllAsync_maps_dto_fields_correctly()
    {
        var result = await _service.GetAllAsync(CancellationToken.None);

        result.Should().AllSatisfy(g =>
        {
            g.Id.Should().NotBeEmpty();
            g.Name.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task CreateFromExternalAsync_creates_new_game_when_no_existing_match()
    {
        var (game, created) = await _service.CreateFromExternalAsync(
            new CreateGameCommand(RawgId: 12345, Name: "New Game", CoverImageUrl: "https://example.com/cover.jpg", Genre: "Action"),
            CancellationToken.None);

        created.Should().BeTrue();
        game.Name.Should().Be("New Game");
        game.CoverImageUrl.Should().Be("https://example.com/cover.jpg");
        game.Genre.Should().Be("Action");
    }

    [Fact]
    public async Task CreateFromExternalAsync_returns_existing_game_when_rawg_id_already_exists()
    {
        var first = await _service.CreateFromExternalAsync(
            new CreateGameCommand(RawgId: 54321, Name: "Duplicate Game", CoverImageUrl: null, Genre: null),
            CancellationToken.None);

        var second = await _service.CreateFromExternalAsync(
            new CreateGameCommand(RawgId: 54321, Name: "Duplicate Game", CoverImageUrl: null, Genre: null),
            CancellationToken.None);

        second.Created.Should().BeFalse();
        second.Game.Id.Should().Be(first.Game.Id);

        var allGames = await _service.GetAllAsync(CancellationToken.None);
        allGames.Count(g => g.Name == "Duplicate Game").Should().Be(1);
    }

    [Fact]
    public async Task CreateFromExternalAsync_returns_seeded_Hollow_Knight_for_its_RAWG_id()
    {
        var (game, created) = await _service.CreateFromExternalAsync(
            new CreateGameCommand(RawgId: 9767, Name: "Hollow Knight", CoverImageUrl: null, Genre: "Platformer"),
            CancellationToken.None);

        created.Should().BeFalse();
        game.Id.Should().Be(HollowKnightGameId);
        (await _dbContext.Games.CountAsync(g => g.Name == "Hollow Knight")).Should().Be(1);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
