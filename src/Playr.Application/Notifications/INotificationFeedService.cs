namespace Playr.Application.Notifications;

public interface INotificationFeedService
{
    Task<NotificationFeedResult> GetPagedAsync(Guid userId, int skip, int take, CancellationToken cancellationToken);
    Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken);
    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Validates <paramref name="mentionedUserIds"/> (drops self-mentions and anyone who
    /// isn't currently a friend of the actor), creates one <see cref="Playr.Domain.Notifications.Notification"/>
    /// row per valid mention, and pushes each live via <see cref="INotificationNotifier"/>.
    /// Returns the filtered, valid list so the caller (PostService/CommentService) can
    /// persist matching PostMention/CommentMention rows for the same set of users - this
    /// method is the single source of truth for "who is allowed to be mentioned".
    /// </summary>
    Task<IReadOnlyList<Guid>> CreateMentionNotificationsAsync(
        Guid actorUserId,
        IReadOnlyCollection<Guid> mentionedUserIds,
        Playr.Domain.Notifications.NotificationType type,
        Guid postId,
        Guid? commentId,
        CancellationToken cancellationToken);
}
