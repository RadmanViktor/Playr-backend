using Playr.Application.Notifications;

namespace Playr.Application.Tests.Notifications;

public sealed class NoOpNotificationFeedService : INotificationFeedService
{
    public Task<NotificationFeedResult> GetPagedAsync(Guid userId, int skip, int take, CancellationToken cancellationToken) =>
        Task.FromResult(new NotificationFeedResult([], false, 0));

    public Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<Guid>> CreateMentionNotificationsAsync(
        Guid actorUserId,
        IReadOnlyCollection<Guid> mentionedUserIds,
        Playr.Domain.Notifications.NotificationType type,
        Guid postId,
        Guid? commentId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>([]);
}
