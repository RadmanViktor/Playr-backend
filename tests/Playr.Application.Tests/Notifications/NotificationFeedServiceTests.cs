using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Domain.Friendships;
using Playr.Domain.Identity;
using Playr.Domain.Notifications;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Notifications;

namespace Playr.Application.Tests.Notifications;

public sealed class NotificationFeedServiceTests : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly PlayrDbContext _dbContext;
    private readonly SpyNotificationNotifier _notifier;
    private readonly NotificationFeedService _service;
    private readonly Guid _actorId;
    private readonly Guid _friendId;
    private readonly Guid _strangerId;
    private readonly Guid _postId = Guid.NewGuid();

    public NotificationFeedServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<PlayrDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dbContext = new PlayrDbContext(options);
        _dbContext.Database.EnsureCreated();

        _actorId = Guid.NewGuid();
        _friendId = Guid.NewGuid();
        _strangerId = Guid.NewGuid();

        AddUser(_actorId, "actor", "Actor");
        AddUser(_friendId, "friend", "Friend");
        AddUser(_strangerId, "stranger", "Stranger");
        _dbContext.Friendships.Add(new Friendship { Id = Guid.NewGuid(), UserAId = _actorId, UserBId = _friendId, CreatedAt = DateTimeOffset.UtcNow });
        _dbContext.SaveChanges();

        _notifier = new SpyNotificationNotifier();
        _service = new NotificationFeedService(_dbContext, _notifier);
    }

    private void AddUser(Guid id, string username, string displayName)
    {
        _dbContext.Users.Add(new ApplicationUser
        {
            Id = id,
            Email = $"{username}@example.com",
            UserName = username,
            NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM",
            NormalizedUserName = username.ToUpperInvariant(),
        });
        _dbContext.UserProfiles.Add(new UserProfile { UserId = id, Username = username, DisplayName = displayName });
    }

    [Fact]
    public async Task CreateMentionNotificationsAsync_creates_notification_for_friend()
    {
        var validIds = await _service.CreateMentionNotificationsAsync(
            _actorId, [_friendId], NotificationType.PostMention, _postId, null, CancellationToken.None);

        validIds.Should().BeEquivalentTo([_friendId]);
        var stored = await _dbContext.Notifications.SingleAsync();
        stored.RecipientUserId.Should().Be(_friendId);
        stored.ActorUserId.Should().Be(_actorId);
        stored.Type.Should().Be(NotificationType.PostMention);
        stored.IsRead.Should().BeFalse();
        _notifier.Notified.Should().ContainSingle(n => n.RecipientUserId == _friendId);
    }

    [Fact]
    public async Task CreateMentionNotificationsAsync_drops_non_friend()
    {
        var validIds = await _service.CreateMentionNotificationsAsync(
            _actorId, [_strangerId], NotificationType.PostMention, _postId, null, CancellationToken.None);

        validIds.Should().BeEmpty();
        (await _dbContext.Notifications.CountAsync()).Should().Be(0);
        _notifier.Notified.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateMentionNotificationsAsync_drops_self_mention()
    {
        var validIds = await _service.CreateMentionNotificationsAsync(
            _actorId, [_actorId], NotificationType.PostMention, _postId, null, CancellationToken.None);

        validIds.Should().BeEmpty();
        (await _dbContext.Notifications.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetPagedAsync_reports_unread_count_and_marks_read()
    {
        await _service.CreateMentionNotificationsAsync(_actorId, [_friendId], NotificationType.PostMention, _postId, null, CancellationToken.None);
        var notificationId = (await _dbContext.Notifications.SingleAsync()).Id;

        var beforeRead = await _service.GetPagedAsync(_friendId, 0, 20, CancellationToken.None);
        beforeRead.UnreadCount.Should().Be(1);
        beforeRead.Items.Should().ContainSingle(n => n.IsRead == false);

        await _service.MarkReadAsync(_friendId, notificationId, CancellationToken.None);

        var afterRead = await _service.GetPagedAsync(_friendId, 0, 20, CancellationToken.None);
        afterRead.UnreadCount.Should().Be(0);
        afterRead.Items.Single().IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAllReadAsync_marks_every_unread_notification_for_the_user()
    {
        await _service.CreateMentionNotificationsAsync(_actorId, [_friendId], NotificationType.PostMention, _postId, null, CancellationToken.None);
        await _service.CreateMentionNotificationsAsync(_actorId, [_friendId], NotificationType.PostMention, Guid.NewGuid(), null, CancellationToken.None);

        await _service.MarkAllReadAsync(_friendId, CancellationToken.None);

        var result = await _service.GetPagedAsync(_friendId, 0, 20, CancellationToken.None);
        result.UnreadCount.Should().Be(0);
        result.Items.Should().OnlyContain(n => n.IsRead);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
