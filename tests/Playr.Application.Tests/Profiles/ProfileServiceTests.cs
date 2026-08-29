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

public sealed class ProfileServiceTests
{
    [Fact]
    public async Task UpdateCurrentUserAsync_WhenDisplayNameIsWhitespace_ThrowsInvalidOperationException()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var command = fixture.ValidCommand() with { DisplayName = "   " };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Display name is required.");
    }

    [Fact]
    public async Task UpdateCurrentUserAsync_WhenDisplayNameIsTooLong_ThrowsInvalidOperationException()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var command = fixture.ValidCommand() with { DisplayName = new string('a', 65) };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Display name cannot be longer than 64 characters.");
    }

    [Theory]
    [InlineData(nameof(UpdateProfileCommand.Bio), 501, "Bio cannot be longer than 500 characters.")]
    [InlineData(nameof(UpdateProfileCommand.Region), 65, "Region cannot be longer than 64 characters.")]
    public async Task UpdateCurrentUserAsync_WhenTextFieldIsTooLongAfterTrim_ThrowsInvalidOperationException(
        string propertyName,
        int length,
        string expectedMessage)
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var value = $" {new string('a', length)} ";
        var command = propertyName switch
        {
            nameof(UpdateProfileCommand.Bio) => fixture.ValidCommand() with { Bio = value },
            nameof(UpdateProfileCommand.Region) => fixture.ValidCommand() with { Region = value },
            _ => throw new InvalidOperationException("Unexpected property name.")
        };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(expectedMessage);
    }

    [Fact]
    public async Task UpdateCurrentUserAsync_WhenListContainsNull_ThrowsInvalidOperationException()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var command = fixture.ValidCommand() with { Languages = new[] { "English", null! } };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Languages cannot contain null values.");
    }

    [Theory]
    [InlineData(nameof(UpdateProfileCommand.Languages))]
    [InlineData(nameof(UpdateProfileCommand.Platforms))]
    [InlineData(nameof(UpdateProfileCommand.CurrentlyPlayingGames))]
    public async Task UpdateCurrentUserAsync_WhenListContainsOversizedItem_ThrowsInvalidOperationException(string propertyName)
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var value = new string('a', 65);
        var command = propertyName switch
        {
            nameof(UpdateProfileCommand.Languages) => fixture.ValidCommand() with { Languages = [value] },
            nameof(UpdateProfileCommand.Platforms) => fixture.ValidCommand() with { Platforms = [value] },
            nameof(UpdateProfileCommand.CurrentlyPlayingGames) => fixture.ValidCommand() with { CurrentlyPlayingGames = [value] },
            _ => throw new InvalidOperationException("Unexpected property name.")
        };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"{propertyName} items cannot be longer than 64 characters.");
    }

    [Theory]
    [InlineData(nameof(UpdateProfileCommand.Languages))]
    [InlineData(nameof(UpdateProfileCommand.Platforms))]
    public async Task UpdateCurrentUserAsync_WhenListContainsTooManyItems_ThrowsInvalidOperationException(string propertyName)
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var values = Enumerable.Range(1, 21).Select(index => $"item-{index}").ToArray();
        var command = propertyName switch
        {
            nameof(UpdateProfileCommand.Languages) => fixture.ValidCommand() with { Languages = values },
            nameof(UpdateProfileCommand.Platforms) => fixture.ValidCommand() with { Platforms = values },
            _ => throw new InvalidOperationException("Unexpected property name.")
        };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"{propertyName} cannot contain more than 20 items.");
    }

    [Fact]
    public async Task UpdateCurrentUserAsync_WhenCurrentlyPlayingGamesContainsTooManyItems_ThrowsInvalidOperationException()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var command = fixture.ValidCommand() with
        {
            CurrentlyPlayingGames = Enumerable.Range(1, 21).Select(index => $"game-{index}").ToArray()
        };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("CurrentlyPlayingGames cannot contain more than 20 items.");
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

    [Fact]
    public async Task UpdateCurrentUserAsync_WhenExternalLinksContainTooManyItems_ThrowsInvalidOperationException()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var command = fixture.ValidCommand() with
        {
            ExternalLinks = Enumerable.Range(1, 11).ToDictionary(index => $"link-{index}", _ => "https://example.com")
        };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("External links cannot contain more than 10 items.");
    }

    [Theory]
    [InlineData("", "https://example.com", "External link keys are required.")]
    [InlineData("   ", "https://example.com", "External link keys are required.")]
    [InlineData("Steam", "", "External link values are required.")]
    [InlineData("Steam", "   ", "External link values are required.")]
    public async Task UpdateCurrentUserAsync_WhenExternalLinkContainsEmptyKeyOrValue_ThrowsInvalidOperationException(
        string key,
        string value,
        string expectedMessage)
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var command = fixture.ValidCommand() with
        {
            ExternalLinks = new Dictionary<string, string> { [key] = value }
        };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(expectedMessage);
    }

    [Theory]
    [InlineData(65, 19, "External link keys cannot be longer than 64 characters.")]
    [InlineData(5, 501, "External link values cannot be longer than 500 characters.")]
    public async Task UpdateCurrentUserAsync_WhenExternalLinkContainsOversizedKeyOrValue_ThrowsInvalidOperationException(
        int keyLength,
        int valueLength,
        string expectedMessage)
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var command = fixture.ValidCommand() with
        {
            ExternalLinks = new Dictionary<string, string> { [new string('k', keyLength)] = new string('v', valueLength) }
        };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage(expectedMessage);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("/profiles/player")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/player")]
    public async Task UpdateCurrentUserAsync_WhenExternalLinkValueIsNotAbsoluteHttpUrl_ThrowsInvalidOperationException(string value)
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var command = fixture.ValidCommand() with
        {
            ExternalLinks = new Dictionary<string, string> { ["Steam"] = value }
        };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("External link values must be absolute HTTP or HTTPS URLs.");
    }

    [Fact]
    public async Task UpdateStatusAsync_persists_trimmed_note_when_looking_for_game()
    {
        var (service, dbContext, userId, gameId) = await CreateServiceWithSeededProfileAndGameAsync();

        var result = await service.UpdateStatusAsync(
            userId,
            new UpdateStatusCommand(ProfileStatus.LookingForGame, gameId, PlayStyle.Chill, "  need a 4th  "),
            CancellationToken.None);

        result.LookingForGameNote.Should().Be("need a 4th");
    }

    [Fact]
    public async Task UpdateStatusAsync_rejects_note_over_max_length()
    {
        var (service, _, userId, gameId) = await CreateServiceWithSeededProfileAndGameAsync();
        var overLong = new string('a', 201);

        var act = () => service.UpdateStatusAsync(
            userId,
            new UpdateStatusCommand(ProfileStatus.LookingForGame, gameId, PlayStyle.Competitive, overLong),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Looking for game note cannot be longer than 200 characters.");
    }

    [Fact]
    public async Task UpdateStatusAsync_stores_null_note_when_not_provided()
    {
        var (service, _, userId, gameId) = await CreateServiceWithSeededProfileAndGameAsync();

        var result = await service.UpdateStatusAsync(
            userId,
            new UpdateStatusCommand(ProfileStatus.LookingForGame, gameId, PlayStyle.Competitive, null),
            CancellationToken.None);

        result.LookingForGameNote.Should().BeNull();
    }

    [Fact]
    public async Task UpdateStatusAsync_clears_note_when_status_changes_away_from_looking_for_game()
    {
        var (service, _, userId, gameId) = await CreateServiceWithSeededProfileAndGameAsync();
        await service.UpdateStatusAsync(
            userId,
            new UpdateStatusCommand(ProfileStatus.LookingForGame, gameId, PlayStyle.Chill, "need a 4th"),
            CancellationToken.None);

        var result = await service.UpdateStatusAsync(
            userId,
            new UpdateStatusCommand(ProfileStatus.Online, null, null, null),
            CancellationToken.None);

        result.LookingForGameNote.Should().BeNull();
    }

    [Fact]
    public async Task SetOfflineAsync_sets_status_to_offline_and_clears_looking_for_game()
    {
        var (service, dbContext, userId, gameId) = await CreateServiceWithSeededProfileAndGameAsync();
        await service.UpdateStatusAsync(
            userId,
            new UpdateStatusCommand(ProfileStatus.LookingForGame, gameId, PlayStyle.Chill, "need a 4th"),
            CancellationToken.None);

        await service.SetOfflineAsync(userId, CancellationToken.None);

        var profile = await dbContext.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == userId);
        profile.Status.Should().Be(ProfileStatus.Offline);
        profile.LookingForGameId.Should().BeNull();
    }

    [Fact]
    public async Task SetOfflineAsync_is_a_no_op_when_already_offline()
    {
        var (service, dbContext, userId, _) = await CreateServiceWithSeededProfileAndGameAsync();
        await service.SetOfflineAsync(userId, CancellationToken.None);
        var updatedAtAfterFirstCall = (await dbContext.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == userId)).UpdatedAt;

        await service.SetOfflineAsync(userId, CancellationToken.None);

        var profile = await dbContext.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == userId);
        profile.Status.Should().Be(ProfileStatus.Offline);
        profile.UpdatedAt.Should().Be(updatedAtAfterFirstCall);
    }

    [Fact]
    public async Task SetOnlineIfOfflineAsync_sets_status_to_online_when_offline()
    {
        var (service, dbContext, userId, _) = await CreateServiceWithSeededProfileAndGameAsync();
        await service.SetOfflineAsync(userId, CancellationToken.None);

        await service.SetOnlineIfOfflineAsync(userId, CancellationToken.None);

        var profile = await dbContext.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == userId);
        profile.Status.Should().Be(ProfileStatus.Online);
    }

    [Fact]
    public async Task SetOnlineIfOfflineAsync_does_not_change_status_when_not_offline()
    {
        var (service, dbContext, userId, _) = await CreateServiceWithSeededProfileAndGameAsync();
        await service.UpdateStatusAsync(userId, new UpdateStatusCommand(ProfileStatus.Busy, null, null, null), CancellationToken.None);

        await service.SetOnlineIfOfflineAsync(userId, CancellationToken.None);

        var profile = await dbContext.UserProfiles.AsNoTracking().FirstAsync(p => p.UserId == userId);
        profile.Status.Should().Be(ProfileStatus.Busy);
    }

    private static async Task<(ProfileService Service, PlayrDbContext DbContext, Guid UserId, Guid GameId)> CreateServiceWithSeededProfileAndGameAsync()
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
        var gameId = Guid.NewGuid();
        dbContext.Games.Add(new Game
        {
            Id = gameId,
            Name = "Chess"
        });
        await dbContext.SaveChangesAsync();
        var service = new ProfileService(dbContext, new Playr.Application.Tests.Posts.NoOpFileStorageService());
        return (service, dbContext, userId, gameId);
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
            return new ProfileFixture(connection, dbContext, new ProfileService(dbContext, new Playr.Application.Tests.Posts.NoOpFileStorageService()), userId);
        }

        public UpdateProfileCommand ValidCommand() => new(
            "Player",
            null,
            null,
            ["English"],
            ["PC"],
            new Dictionary<string, string> { ["Steam"] = "https://example.com/player" },
            ["Chess"]);

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
