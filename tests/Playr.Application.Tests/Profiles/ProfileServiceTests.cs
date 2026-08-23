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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/avatars/player.png")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/avatar.png")]
    public async Task UpdateCurrentUserAsync_WhenAvatarUrlIsNotAbsoluteHttpUrl_ThrowsInvalidOperationException(string avatarUrl)
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var command = fixture.ValidCommand() with { AvatarUrl = avatarUrl };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Avatar URL must be an absolute HTTP or HTTPS URL.");
    }

    [Fact]
    public async Task UpdateCurrentUserAsync_WhenAvatarUrlIsTooLongAfterTrim_ThrowsInvalidOperationException()
    {
        await using var fixture = await ProfileFixture.CreateAsync();
        var command = fixture.ValidCommand() with { AvatarUrl = $" https://example.com/{new string('a', 481)} " };

        var act = () => fixture.Service.UpdateCurrentUserAsync(fixture.UserId, command, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Avatar URL cannot be longer than 500 characters.");
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
            ["Chess"]);

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
