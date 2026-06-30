using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Profiles;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Profiles;

namespace Playr.Application.Tests.Profiles;

public sealed class ProfileServiceTests
{
    [Fact]
    public async Task UpdateCurrentUserAsync_WhenListContainsNull_ThrowsInvalidOperationException()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var command = fixture.ValidCommand() with { Languages = new[] { "English", null! } };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Languages cannot contain null values.");
    }

    [Fact]
    public async Task UpdateCurrentUserAsync_WhenExternalLinkKeysNormalizeToDuplicate_ThrowsInvalidOperationException()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var command = fixture.ValidCommand() with
        {
            ExternalLinks = new Dictionary<string, string>
            {
                ["Steam"] = "https://example.com/one",
                [" steam "] = "https://example.com/two"
            }
        };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("External links cannot contain duplicate keys.");
    }

    [Fact]
    public async Task UpdateCurrentUserAsync_WhenExternalLinkValueIsNull_ThrowsInvalidOperationException()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var command = fixture.ValidCommand() with
        {
            ExternalLinks = new Dictionary<string, string> { ["Steam"] = null! }
        };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("External links cannot contain null keys or values.");
    }

    private sealed class ProfileFixture : IAsyncDisposable
    {
        private ProfileFixture(SqliteConnection connection, PlayrDbContext dbContext, ProfileService service, Guid userId)
        {
            Connection = connection;
            DbContext = dbContext;
            Service = service;
            UserId = userId;
        }

        public SqliteConnection Connection { get; }
        public PlayrDbContext DbContext { get; }
        public ProfileService Service { get; }
        public Guid UserId { get; }

        public static async Task<ProfileFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PlayrDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new PlayrDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            var userId = Guid.NewGuid();
            dbContext.Users.Add(new ApplicationUser
            {
                Id = userId,
                Email = "player@example.com",
                UserName = "player",
                NormalizedEmail = "PLAYER@EXAMPLE.COM",
                NormalizedUserName = "PLAYER"
            });
            dbContext.UserProfiles.Add(new UserProfile
            {
                UserId = userId,
                Username = "player",
                DisplayName = "Player"
            });
            await dbContext.SaveChangesAsync();
            return new ProfileFixture(connection, dbContext, new ProfileService(dbContext), userId);
        }

        public UpdateProfileCommand ValidCommand() => new(
            "Player",
            null,
            null,
            null,
            ["English"],
            ["PC"],
            new Dictionary<string, string> { ["Steam"] = "https://example.com/player" },
            ["Chess"],
            true);

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
