using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Profiles;
using Playr.Domain.Games;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Profiles;

namespace Playr.Application.Tests.Profiles;

public sealed class PublicLookingForGameServiceTests
{
    [Fact]
    public async Task GetSummaryAsync_returns_ranked_limited_summary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new PlayrDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var valorant = new Game { Id = Guid.NewGuid(), Name = "Valorant", CoverImageUrl = "/covers/valorant.jpg" };
        var chess = new Game { Id = Guid.NewGuid(), Name = "Chess" };
        dbContext.Games.AddRange(valorant, chess);

        AddProfile("alpha", ProfileStatus.LookingForGame, valorant, PlayStyle.Chill, 10, "/avatars/alpha.jpg");
        AddProfile("bravo", ProfileStatus.LookingForGame, valorant, PlayStyle.Competitive, 12);
        AddProfile("delta", ProfileStatus.LookingForGame, chess, PlayStyle.Chill, 11);
        AddProfile("echo", ProfileStatus.LookingForGame, chess, PlayStyle.Competitive, 9);
        AddProfile("foxtrot", ProfileStatus.LookingForGame, valorant, PlayStyle.Chill, 8);
        AddProfile("charlie", ProfileStatus.Online, chess, PlayStyle.Chill, 13);
        AddProfile("broken", ProfileStatus.LookingForGame, null, null, 14);
        await dbContext.SaveChangesAsync();

        var service = new PublicLookingForGameService(dbContext);
        var result = await service.GetSummaryAsync(CancellationToken.None);

        result.TotalCount.Should().Be(5);
        result.Players.Select(player => player.Username).Should().Equal("bravo", "delta", "alpha");
        result.Players[2].AvatarUrl.Should().Be("/avatars/alpha.jpg");
        result.Players[2].PlayStyle.Should().Be(PlayStyle.Chill);
        result.FeaturedGame.Should().BeEquivalentTo(
            new PublicLookingForGameFeaturedGameDto("Valorant", "/covers/valorant.jpg", 3));

        void AddProfile(
            string username,
            ProfileStatus status,
            Game? game,
            PlayStyle? playStyle,
            int hour,
            string? avatarUrl = null)
        {
            var userId = Guid.NewGuid();
            dbContext.Users.Add(new ApplicationUser
            {
                Id = userId,
                Email = $"{username}@example.com",
                UserName = username,
                NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM",
                NormalizedUserName = username.ToUpperInvariant(),
            });
            dbContext.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Username = username,
                DisplayName = char.ToUpperInvariant(username[0]) + username[1..],
                AvatarUrl = avatarUrl,
                Status = status,
                LookingForGameId = game?.Id,
                LookingForPlayStyle = playStyle,
                UpdatedAt = new DateTimeOffset(2026, 9, 2, hour, 0, 0, TimeSpan.Zero),
            });
        }
    }

    [Fact]
    public async Task GetSummaryAsync_returns_empty_summary()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new PlayrDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        var result = await new PublicLookingForGameService(dbContext)
            .GetSummaryAsync(CancellationToken.None);

        result.Should().BeEquivalentTo(new PublicLookingForGameSummaryDto(0, null, []));
    }
}
