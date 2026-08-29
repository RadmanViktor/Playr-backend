using Microsoft.EntityFrameworkCore;
using Playr.Application.Notifications;
using Playr.Domain.Notifications;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Notifications;

public sealed class NotificationFeedService(PlayrDbContext dbContext, INotificationNotifier notifier) : INotificationFeedService
{
    private const int MaxTake = 50;

    public async Task<NotificationFeedResult> GetPagedAsync(Guid userId, int skip, int take, CancellationToken cancellationToken)
    {
        var effectiveTake = take <= 0 ? 20 : Math.Min(take, MaxTake);
        var effectiveSkip = Math.Max(skip, 0);

        var notifications = await dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(effectiveSkip)
            .Take(effectiveTake + 1)
            .ToListAsync(cancellationToken);

        var hasMore = notifications.Count > effectiveTake;
        if (hasMore)
        {
            notifications.RemoveAt(notifications.Count - 1);
        }

        var unreadCount = await dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientUserId == userId && !n.IsRead, cancellationToken);

        var actorIds = notifications.Select(n => n.ActorUserId).Distinct().ToList();
        var profiles = await dbContext.UserProfiles
            .AsNoTracking()
            .Where(p => actorIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var profileMap = profiles.ToDictionary(p => p.UserId);

        var dtos = notifications.Select(n =>
        {
            var actor = profileMap[n.ActorUserId];
            return new NotificationDto(
                n.Id,
                n.Type.ToString(),
                n.IsRead,
                n.CreatedAt,
                new NotificationActorDto(actor.UserId, actor.Username, actor.DisplayName, actor.AvatarUrl),
                n.RecipientUserId,
                n.PostId,
                n.CommentId);
        }).ToList();

        return new NotificationFeedResult(dtos, hasMore, unreadCount);
    }

    public async Task MarkReadAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Notification was not found.");

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var unread = await dbContext.Notifications
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
        {
            return;
        }

        foreach (var notification in unread)
        {
            notification.IsRead = true;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> CreateMentionNotificationsAsync(
        Guid actorUserId,
        IReadOnlyCollection<Guid> mentionedUserIds,
        NotificationType type,
        Guid postId,
        Guid? commentId,
        CancellationToken cancellationToken)
    {
        var candidateIds = mentionedUserIds.Distinct().Where(id => id != actorUserId).ToList();
        if (candidateIds.Count == 0)
        {
            return [];
        }

        var friendships = await dbContext.Friendships
            .AsNoTracking()
            .Where(f => f.UserAId == actorUserId || f.UserBId == actorUserId)
            .ToListAsync(cancellationToken);
        var friendIds = friendships
            .Select(f => f.UserAId == actorUserId ? f.UserBId : f.UserAId)
            .ToHashSet();

        var validIds = candidateIds.Where(friendIds.Contains).ToList();
        if (validIds.Count == 0)
        {
            return [];
        }

        var actorProfile = await dbContext.UserProfiles
            .AsNoTracking()
            .FirstAsync(p => p.UserId == actorUserId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var notifications = validIds.Select(recipientId => new Notification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientId,
            ActorUserId = actorUserId,
            Type = type,
            PostId = postId,
            CommentId = commentId,
            IsRead = false,
            CreatedAt = now,
        }).ToList();

        dbContext.Notifications.AddRange(notifications);
        await dbContext.SaveChangesAsync(cancellationToken);

        var actorDto = new NotificationActorDto(actorProfile.UserId, actorProfile.Username, actorProfile.DisplayName, actorProfile.AvatarUrl);
        foreach (var notification in notifications)
        {
            var dto = new NotificationDto(
                notification.Id,
                notification.Type.ToString(),
                notification.IsRead,
                notification.CreatedAt,
                actorDto,
                notification.RecipientUserId,
                notification.PostId,
                notification.CommentId);
            await notifier.NotifyNotificationCreatedAsync(dto, cancellationToken);
        }

        return validIds;
    }
}
