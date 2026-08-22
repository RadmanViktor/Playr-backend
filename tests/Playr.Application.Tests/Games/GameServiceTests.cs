using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Games;
using Playr.Domain.Games;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Games;

namespace Playr.Application.Tests.Games;

public sealed class GameServiceTests : IAsyncDisposable
{
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
        _service = new GameService(_dbContext);
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

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
