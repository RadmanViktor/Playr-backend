using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Notifications;

namespace Playr.Application.Tests.Notifications;

public sealed class NotificationPreferencesServiceTests
{
    [Fact]
    public async Task GetAsync_DefaultsToEnabled()
    {
        await using var fixture = await Fixture.CreateAsync();

        var preferences = await fixture.Service.GetAsync(fixture.UserId, CancellationToken.None);

        preferences.ChatSoundEnabled.Should().BeTrue();
        preferences.ChatBrowserNotificationsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_PersistsPreferences()
    {
        await using var fixture = await Fixture.CreateAsync();

        var updated = await fixture.Service.UpdateAsync(
            fixture.UserId,
            new(ChatSoundEnabled: false, ChatBrowserNotificationsEnabled: false),
            CancellationToken.None);

        updated.ChatSoundEnabled.Should().BeFalse();
        updated.ChatBrowserNotificationsEnabled.Should().BeFalse();

        var reloaded = await fixture.Service.GetAsync(fixture.UserId, CancellationToken.None);
        reloaded.ChatSoundEnabled.Should().BeFalse();
        reloaded.ChatBrowserNotificationsEnabled.Should().BeFalse();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(SqliteConnection connection, PlayrDbContext dbContext, NotificationPreferencesService service)
        {
            Connection = connection;
            DbContext = dbContext;
            Service = service;
        }

        public SqliteConnection Connection { get; }
        public PlayrDbContext DbContext { get; }
        public NotificationPreferencesService Service { get; }
        public Guid UserId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000001");

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PlayrDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new PlayrDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            var fixture = new Fixture(connection, dbContext, new NotificationPreferencesService(dbContext));

            dbContext.Users.Add(new ApplicationUser
            {
                Id = fixture.UserId,
                Email = "player@example.com",
                UserName = "player",
                NormalizedEmail = "PLAYER@EXAMPLE.COM",
                NormalizedUserName = "PLAYER"
            });
            dbContext.UserProfiles.Add(new UserProfile
            {
                UserId = fixture.UserId,
                Username = "player",
                DisplayName = "Player"
            });
            await dbContext.SaveChangesAsync();

            return fixture;
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
