using Microsoft.EntityFrameworkCore;
using Playr.Application.Chat;
using Playr.Domain.Chat;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Chat;

public sealed class ChatService(PlayrDbContext dbContext) : IChatService
{
    private const int MaxMessageLength = 1000;

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var conversations = await dbContext.Conversations.AsNoTracking()
            .Where(c => c.DirectUserAId == userId || c.DirectUserBId == userId)
            .OrderByDescending(c => c.UpdatedAt)
            .ToListAsync(cancellationToken);

        return await MapConversationsAsync(conversations, userId, cancellationToken);
    }

    public async Task<ConversationDto> GetOrCreateDirectConversationAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken)
    {
        if (userId == otherUserId)
        {
            throw new InvalidOperationException("You cannot start a conversation with yourself.");
        }

        if (!await AreFriendsAsync(userId, otherUserId, cancellationToken))
        {
            throw new InvalidOperationException("You can only chat with friends.");
        }

        var (userAId, userBId) = OrderPair(userId, otherUserId);
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(c => c.DirectUserAId == userAId && c.DirectUserBId == userBId, cancellationToken);

        if (conversation is null)
        {
            var now = DateTimeOffset.UtcNow;
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                DirectUserAId = userAId,
                DirectUserBId = userBId,
                CreatedAt = now,
                UpdatedAt = now,
                Participants =
                [
                    new ConversationParticipant { UserId = userAId, JoinedAt = now },
                    new ConversationParticipant { UserId = userBId, JoinedAt = now }
                ]
            };
            dbContext.Conversations.Add(conversation);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var dtos = await MapConversationsAsync([conversation], userId, cancellationToken);
        return dtos[0];
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        await EnsureParticipantAsync(userId, conversationId, cancellationToken);

        var messages = await dbContext.ChatMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return await MapMessagesAsync(messages, cancellationToken);
    }

    public async Task<ChatMessageDto> SendMessageAsync(
        Guid userId,
        Guid conversationId,
        SendChatMessageCommand command,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken)
            ?? throw new InvalidOperationException("Conversation was not found.");

        if (conversation.DirectUserAId != userId && conversation.DirectUserBId != userId)
        {
            throw new InvalidOperationException("You are not part of this conversation.");
        }

        var body = command.Body?.Trim() ?? string.Empty;
        if (body.Length == 0)
        {
            throw new InvalidOperationException("Message is required.");
        }

        if (body.Length > MaxMessageLength)
        {
            throw new InvalidOperationException($"Message cannot be longer than {MaxMessageLength} characters.");
        }

        var now = DateTimeOffset.UtcNow;
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderUserId = userId,
            Body = body,
            CreatedAt = now
        };
        conversation.UpdatedAt = now;
        dbContext.ChatMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dtos = await MapMessagesAsync([message], cancellationToken);
        return dtos[0];
    }

    private async Task EnsureParticipantAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var isParticipant = await dbContext.Conversations.AsNoTracking()
            .AnyAsync(c => c.Id == conversationId && (c.DirectUserAId == userId || c.DirectUserBId == userId), cancellationToken);
        if (!isParticipant)
        {
            throw new InvalidOperationException("You are not part of this conversation.");
        }
    }

    private async Task<bool> AreFriendsAsync(Guid userId1, Guid userId2, CancellationToken cancellationToken)
    {
        var (userAId, userBId) = OrderPair(userId1, userId2);
        return await dbContext.Friendships.AsNoTracking()
            .AnyAsync(f => f.UserAId == userAId && f.UserBId == userBId, cancellationToken);
    }

    private async Task<IReadOnlyList<ConversationDto>> MapConversationsAsync(
        IList<Conversation> conversations,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        if (conversations.Count == 0)
        {
            return [];
        }

        var otherUserIds = conversations
            .Select(c => c.DirectUserAId == currentUserId ? c.DirectUserBId : c.DirectUserAId)
            .ToList();
        var profiles = await dbContext.UserProfiles.AsNoTracking()
            .Where(p => otherUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var profileMap = profiles.ToDictionary(p => p.UserId);

        var conversationIds = conversations.Select(c => c.Id).ToList();
        var lastMessages = await dbContext.ChatMessages.AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.OrderByDescending(m => m.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var lastMessageMap = lastMessages.ToDictionary(m => m.ConversationId);

        return conversations.Select(conversation =>
        {
            var otherUserId = conversation.DirectUserAId == currentUserId ? conversation.DirectUserBId : conversation.DirectUserAId;
            var profile = profileMap[otherUserId];
            lastMessageMap.TryGetValue(conversation.Id, out var lastMessage);
            return new ConversationDto(
                conversation.Id,
                new ChatParticipantDto(profile.UserId, profile.Username, profile.DisplayName, profile.AvatarUrl),
                lastMessage?.Body,
                lastMessage?.CreatedAt,
                conversation.CreatedAt,
                conversation.UpdatedAt);
        }).ToList();
    }

    private async Task<IReadOnlyList<ChatMessageDto>> MapMessagesAsync(IList<ChatMessage> messages, CancellationToken cancellationToken)
    {
        if (messages.Count == 0)
        {
            return [];
        }

        var senderIds = messages.Select(m => m.SenderUserId).Distinct().ToList();
        var profiles = await dbContext.UserProfiles.AsNoTracking()
            .Where(p => senderIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var profileMap = profiles.ToDictionary(p => p.UserId);

        return messages.Select(message =>
        {
            var profile = profileMap[message.SenderUserId];
            return new ChatMessageDto(
                message.Id,
                message.ConversationId,
                message.SenderUserId,
                profile.Username,
                profile.DisplayName,
                profile.AvatarUrl,
                message.Body,
                message.CreatedAt,
                message.ReadAt);
        }).ToList();
    }

    private static (Guid UserAId, Guid UserBId) OrderPair(Guid userId1, Guid userId2) =>
        userId1.CompareTo(userId2) < 0 ? (userId1, userId2) : (userId2, userId1);
}
