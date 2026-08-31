using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Tests.Notifications;
using Playr.Domain.Identity;
using Playr.Domain.Notifications;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Follows;
using Playr.Infrastructure.Notifications;

namespace Playr.Application.Tests.Follows;

public sealed class FollowServiceTests
{
    [Fact]
    public async Task FollowAsync_ToSelf_Throws()
    {
        await using var fixture = await FollowFixture.CreateAsync();

        var act = () => fixture.Service.FollowAsync(fixture.CurrentUserId, fixture.CurrentUserId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("You cannot follow yourself.");
    }

    [Fact]
    public async Task FollowAsync_WhenTargetDoesNotExist_Throws()
    {
        await using var fixture = await FollowFixture.CreateAsync();

        var act = () => fixture.Service.FollowAsync(fixture.CurrentUserId, Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Player was not found.");
    }

    [Fact]
    public async Task FollowAsync_CreatesFollow_AndIsFollowingReturnsTrue()
    {
        await using var fixture = await FollowFixture.CreateAsync();

        var follow = await fixture.Service.FollowAsync(fixture.CurrentUserId, fixture.OtherUserId, CancellationToken.None);

        follow.UserId.Should().Be(fixture.OtherUserId);
        (await fixture.Service.IsFollowingAsync(fixture.CurrentUserId, fixture.OtherUserId, CancellationToken.None))
            .Should().BeTrue();
        fixture.DbContext.UserFollows.Should().HaveCount(1);
    }

    [Fact]
    public async Task FollowAsync_WhenAlreadyFollowing_IsIdempotent()
    {
        await using var fixture = await FollowFixture.CreateAsync();
        await fixture.Service.FollowAsync(fixture.CurrentUserId, fixture.OtherUserId, CancellationToken.None);

        await fixture.Service.FollowAsync(fixture.CurrentUserId, fixture.OtherUserId, CancellationToken.None);

        fixture.DbContext.UserFollows.Should().HaveCount(1);
    }

    [Fact]
    public async Task UnfollowAsync_RemovesFollow()
    {
        await using var fixture = await FollowFixture.CreateAsync();
        await fixture.Service.FollowAsync(fixture.CurrentUserId, fixture.OtherUserId, CancellationToken.None);

        await fixture.Service.UnfollowAsync(fixture.CurrentUserId, fixture.OtherUserId, CancellationToken.None);

        fixture.DbContext.UserFollows.Should().BeEmpty();
        (await fixture.Service.IsFollowingAsync(fixture.CurrentUserId, fixture.OtherUserId, CancellationToken.None))
            .Should().BeFalse();
    }

    [Fact]
    public async Task UnfollowAsync_WhenNotFollowing_DoesNotThrow()
    {
        await using var fixture = await FollowFixture.CreateAsync();

        var act = () => fixture.Service.UnfollowAsync(fixture.CurrentUserId, fixture.OtherUserId, CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetCountsAsync_ReturnsFollowerAndFollowingCounts()
    {
        await using var fixture = await FollowFixture.CreateAsync();
        await fixture.Service.FollowAsync(fixture.CurrentUserId, fixture.OtherUserId, CancellationToken.None);

        var currentUserCounts = await fixture.Service.GetCountsAsync(fixture.CurrentUserId, CancellationToken.None);
        var otherUserCounts = await fixture.Service.GetCountsAsync(fixture.OtherUserId, CancellationToken.None);

        currentUserCounts.FollowingCount.Should().Be(1);
        currentUserCounts.FollowersCount.Should().Be(0);
        otherUserCounts.FollowersCount.Should().Be(1);
        otherUserCounts.FollowingCount.Should().Be(0);
    }

    [Fact]
    public async Task GetFollowersAsync_And_GetFollowingAsync_ReturnHydratedResults()
    {
        await using var fixture = await FollowFixture.CreateAsync();
        await fixture.Service.FollowAsync(fixture.CurrentUserId, fixture.OtherUserId, CancellationToken.None);

        var followers = await fixture.Service.GetFollowersAsync(fixture.OtherUserId, CancellationToken.None);
        var following = await fixture.Service.GetFollowingAsync(fixture.CurrentUserId, CancellationToken.None);

        followers.Should().ContainSingle(f => f.UserId == fixture.CurrentUserId);
        following.Should().ContainSingle(f => f.UserId == fixture.OtherUserId);
    }

    [Fact]
    public async Task FollowAsync_CreatesNewFollowerNotification_ForFollowedUser()
    {
        await using var fixture = await FollowFixture.CreateAsync();

        await fixture.Service.FollowAsync(fixture.CurrentUserId, fixture.OtherUserId, CancellationToken.None);

        var notification = await fixture.DbContext.Notifications.SingleAsync();
        notification.RecipientUserId.Should().Be(fixture.OtherUserId);
        notification.ActorUserId.Should().Be(fixture.CurrentUserId);
        notification.Type.Should().Be(NotificationType.NewFollower);
        notification.IsRead.Should().BeFalse();
        fixture.Notifier.Notified.Should().ContainSingle(n => n.RecipientUserId == fixture.OtherUserId);
    }

    [Fact]
    public async Task FollowAsync_WhenAlreadyFollowing_DoesNotCreateDuplicateNotification()
    {
        await using var fixture = await FollowFixture.CreateAsync();
        await fixture.Service.FollowAsync(fixture.CurrentUserId, fixture.OtherUserId, CancellationToken.None);

        await fixture.Service.FollowAsync(fixture.CurrentUserId, fixture.OtherUserId, CancellationToken.None);

        (await fixture.DbContext.Notifications.CountAsync()).Should().Be(1);
    }

    private sealed class FollowFixture : IAsyncDisposable
    {
        private FollowFixture(SqliteConnection connection, PlayrDbContext dbContext, FollowService service, SpyNotificationNotifier notifier)
        {
            Connection = connection;
            DbContext = dbContext;
            Service = service;
            Notifier = notifier;
        }

        public SqliteConnection Connection { get; }
        public PlayrDbContext DbContext { get; }
        public FollowService Service { get; }
        public SpyNotificationNotifier Notifier { get; }
        public Guid CurrentUserId { get; } = Guid.Parse("40000000-0000-0000-0000-000000000001");
        public Guid OtherUserId { get; } = Guid.Parse("40000000-0000-0000-0000-000000000002");

        public static async Task<FollowFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PlayrDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new PlayrDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            var notifier = new SpyNotificationNotifier();
            var notificationFeedService = new NotificationFeedService(dbContext, notifier);
            var fixture = new FollowFixture(connection, dbContext, new FollowService(dbContext, new NoOpFollowNotifier(), notificationFeedService), notifier);
            fixture.AddUser(fixture.CurrentUserId, "player", "Player");
            fixture.AddUser(fixture.OtherUserId, "friend", "Friend");
            await dbContext.SaveChangesAsync();
            return fixture;
        }

        private void AddUser(Guid id, string username, string displayName)
        {
            DbContext.Users.Add(new ApplicationUser
            {
                Id = id,
                Email = $"{username}@example.com",
                UserName = username,
                NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM",
                NormalizedUserName = username.ToUpperInvariant()
            });
            DbContext.UserProfiles.Add(new UserProfile
            {
                UserId = id,
                Username = username,
                DisplayName = displayName
            });
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
