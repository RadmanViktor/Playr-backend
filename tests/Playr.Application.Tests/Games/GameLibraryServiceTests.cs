using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Badges;
using Playr.Application.Games;
using Playr.Domain.Badges;
using Playr.Domain.Games;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Games;

namespace Playr.Application.Tests.Games;

public sealed class GameLibraryServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly SpyBadgeService _badgeService;
    private readonly GameLibraryService _service;
    private static readonly Guid GameId = new("00000001-0000-0000-0000-000000000001"); // Apex Legends (seeded)
    private static readonly Guid OtherGameId = new("00000001-0000-0000-0000-000000000002"); // Call of Duty (seeded)
    private static readonly Guid HollowKnightGameId = new("00000001-0000-0000-0000-000000000007");
    private static readonly Guid UserId = Guid.NewGuid();

    public GameLibraryServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();
        _badgeService = new SpyBadgeService();
        _service = new GameLibraryService(_dbContext, _badgeService, Microsoft.Extensions.Logging.Abstractions.NullLogger<GameLibraryService>.Instance);
    }

    [Fact]
    public async Task AddGameAsync_adds_game_with_no_rating()
    {
        var entry = await _service.AddGameAsync(UserId, GameId, CancellationToken.None);

        entry.GameId.Should().Be(GameId);
        entry.Rating.Should().BeNull();
        entry.ReviewText.Should().BeNull();

        var library = await _service.GetLibraryAsync(UserId, CancellationToken.None);
        library.Should().ContainSingle(e => e.GameId == GameId);
    }

    [Fact]
    public async Task AddGameAsync_throws_when_game_already_in_library()
    {
        await _service.AddGameAsync(UserId, GameId, CancellationToken.None);

        var act = () => _service.AddGameAsync(UserId, GameId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AddGameAsync_throws_when_game_does_not_exist()
    {
        var act = () => _service.AddGameAsync(UserId, Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RateGameAsync_throws_when_game_not_in_library()
    {
        var act = () => _service.RateGameAsync(UserId, GameId, 5, "Great!", CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task RateGameAsync_throws_when_rating_is_out_of_range(int rating)
    {
        await _service.AddGameAsync(UserId, GameId, CancellationToken.None);

        var act = () => _service.RateGameAsync(UserId, GameId, rating, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task RateGameAsync_sets_rating_and_review_and_is_updatable()
    {
        await _service.AddGameAsync(UserId, GameId, CancellationToken.None);

        var first = await _service.RateGameAsync(UserId, GameId, 4, "Pretty good", CancellationToken.None);
        first.Rating.Should().Be(4);
        first.ReviewText.Should().Be("Pretty good");

        var updated = await _service.RateGameAsync(UserId, GameId, 5, "Actually amazing", CancellationToken.None);
        updated.Rating.Should().Be(5);
        updated.ReviewText.Should().Be("Actually amazing");

        var library = await _service.GetLibraryAsync(UserId, CancellationToken.None);
        library.Should().ContainSingle(e => e.GameId == GameId && e.Rating == 5);
    }

    [Fact]
    public async Task RateGameAsync_grants_Voidtouched_for_five_stars_on_seeded_Hollow_Knight()
    {
        await _service.AddGameAsync(UserId, HollowKnightGameId, CancellationToken.None);

        await _service.RateGameAsync(UserId, HollowKnightGameId, 5, null, CancellationToken.None);

        _badgeService.Grants.Should().ContainSingle(grant =>
            grant.UserId == UserId && grant.Type.ToString() == "Voidtouched" && grant.Level == BadgeLevel.Gold);
        _badgeService.Checks.Should().Contain((UserId, BadgeType.GameCritic));
    }

    [Fact]
    public async Task RateGameAsync_grants_Voidtouched_for_five_stars_on_RAWG_Hollow_Knight()
    {
        var importedGameId = Guid.NewGuid();
        _dbContext.Games.Add(new Game
        {
            Id = importedGameId,
            Name = "Hollow Knight",
            RawgId = 9767,
        });
        await _dbContext.SaveChangesAsync();
        await _service.AddGameAsync(UserId, importedGameId, CancellationToken.None);

        await _service.RateGameAsync(UserId, importedGameId, 5, null, CancellationToken.None);

        _badgeService.Grants.Should().ContainSingle(grant => grant.Type.ToString() == "Voidtouched");
    }

    [Fact]
    public async Task RateGameAsync_does_not_grant_Voidtouched_below_five_stars()
    {
        await _service.AddGameAsync(UserId, HollowKnightGameId, CancellationToken.None);

        await _service.RateGameAsync(UserId, HollowKnightGameId, 4, null, CancellationToken.None);

        _badgeService.Grants.Should().BeEmpty();
    }

    [Fact]
    public async Task RateGameAsync_does_not_grant_Voidtouched_for_another_game()
    {
        await _service.AddGameAsync(UserId, GameId, CancellationToken.None);

        await _service.RateGameAsync(UserId, GameId, 5, null, CancellationToken.None);

        _badgeService.Grants.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveGameAsync_removes_entry_from_library()
    {
        await _service.AddGameAsync(UserId, GameId, CancellationToken.None);

        await _service.RemoveGameAsync(UserId, GameId, CancellationToken.None);

        var library = await _service.GetLibraryAsync(UserId, CancellationToken.None);
        library.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveGameAsync_throws_when_entry_does_not_exist()
    {
        var act = () => _service.RemoveGameAsync(UserId, GameId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetLibraryAsync_only_returns_entries_for_the_given_user()
    {
        await _service.AddGameAsync(UserId, GameId, CancellationToken.None);
        await _service.AddGameAsync(Guid.NewGuid(), OtherGameId, CancellationToken.None);

        var library = await _service.GetLibraryAsync(UserId, CancellationToken.None);

        library.Should().ContainSingle(e => e.GameId == GameId);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private sealed class SpyBadgeService : IBadgeService
    {
        public List<(Guid UserId, BadgeType Type)> Checks { get; } = [];
        public List<(Guid UserId, BadgeType Type, BadgeLevel Level)> Grants { get; } = [];

        public Task<UserBadgesDto> GetBadgesAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(new UserBadgesDto(userId, [], null, null));

        public Task SetActiveBadgeAsync(Guid userId, BadgeType? badgeType, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task CheckAndUnlockBadgesAsync(Guid userId, BadgeType type, CancellationToken cancellationToken)
        {
            Checks.Add((userId, type));
            return Task.CompletedTask;
        }

        public Task CheckFirstHundredUsersAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task GrantBadgeAsync(
            Guid userId, BadgeType type, BadgeLevel level, CancellationToken cancellationToken)
        {
            Grants.Add((userId, type, level));
            return Task.CompletedTask;
        }

        public Task CheckVeteranStatusAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
