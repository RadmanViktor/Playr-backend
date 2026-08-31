using Microsoft.EntityFrameworkCore;
using Playr.Application.Chat;
using Playr.Application.Storage;
using Playr.Domain.Chat;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Chat;

public sealed class ChatService(PlayrDbContext dbContext, IChatNotifier chatNotifier, IFileStorageService fileStorageService) : IChatService
{
    private const int MaxMessageLength = 1000;
    private const string MediaSubFolder = "chat";

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var conversationIds = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => p.ConversationId)
            .ToListAsync(cancellationToken);

        var conversations = await dbContext.Conversations.AsNoTracking()
            .Where(c => conversationIds.Contains(c.Id))
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

        var (userAId, userBId) = OrderPair(userId, otherUserId);
        var conversation = await dbContext.Conversations
            .FirstOrDefaultAsync(c => c.Type == ConversationType.Direct && c.DirectUserAId == userAId && c.DirectUserBId == userBId, cancellationToken);

        if (conversation is null)
        {
            var now = DateTimeOffset.UtcNow;
            conversation = new Conversation
            {
                Id = Guid.NewGuid(),
                Type = ConversationType.Direct,
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

    public async Task<ConversationDto> GetOrCreateGroupConversationAsync(
        IReadOnlyList<Guid> memberUserIds,
        string? title,
        Guid? lfgGroupId,
        CancellationToken cancellationToken)
    {
        var distinctMemberIds = memberUserIds.Distinct().ToList();
        if (distinctMemberIds.Count < 2)
        {
            throw new InvalidOperationException("A group conversation requires at least two members.");
        }

        if (lfgGroupId is not null)
        {
            var existing = await dbContext.Conversations
                .FirstOrDefaultAsync(c => c.Type == ConversationType.Group && c.LfgGroupId == lfgGroupId, cancellationToken);
            if (existing is not null)
            {
                var existingDtos = await MapConversationsAsync([existing], distinctMemberIds[0], cancellationToken);
                return existingDtos[0];
            }
        }

        var now = DateTimeOffset.UtcNow;
        var conversation = new Conversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Group,
            Title = title,
            LfgGroupId = lfgGroupId,
            CreatedAt = now,
            UpdatedAt = now,
            Participants = distinctMemberIds
                .Select(userId => new ConversationParticipant { UserId = userId, JoinedAt = now })
                .ToList()
        };
        dbContext.Conversations.Add(conversation);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dtos = await MapConversationsAsync([conversation], distinctMemberIds[0], cancellationToken);
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

        var participantIds = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(p => p.ConversationId == conversationId)
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        if (!participantIds.Contains(userId))
        {
            throw new InvalidOperationException("You are not part of this conversation.");
        }

        var body = command.Body?.Trim() ?? string.Empty;
        if (body.Length > MaxMessageLength)
        {
            throw new InvalidOperationException($"Message cannot be longer than {MaxMessageLength} characters.");
        }

        string? mediaUrl = null;
        ChatMediaType? mediaType = null;
        if (command.Media is not null)
        {
            var (validatedType, extension) = ChatMediaValidator.Validate(command.Media);
            var saved = await fileStorageService.SaveAsync(command.Media.Content, extension, MediaSubFolder, cancellationToken);
            mediaUrl = saved.RelativeUrl;
            mediaType = validatedType;
        }

        if (body.Length == 0 && mediaUrl is null)
        {
            throw new InvalidOperationException("Message is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderUserId = userId,
            Body = body,
            MediaUrl = mediaUrl,
            MediaType = mediaType,
            CreatedAt = now
        };
        conversation.UpdatedAt = now;
        dbContext.ChatMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dtos = await MapMessagesAsync([message], cancellationToken);
        var dto = dtos[0];

        await chatNotifier.NotifyNewMessageAsync(
            participantIds,
            dto,
            cancellationToken);

        return dto;
    }

    private async Task EnsureParticipantAsync(Guid userId, Guid conversationId, CancellationToken cancellationToken)
    {
        var isParticipant = await dbContext.ConversationParticipants.AsNoTracking()
            .AnyAsync(p => p.ConversationId == conversationId && p.UserId == userId, cancellationToken);
        if (!isParticipant)
        {
            throw new InvalidOperationException("You are not part of this conversation.");
        }
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

        var conversationIds = conversations.Select(c => c.Id).ToList();

        var directConversationIds = conversations.Where(c => c.Type == ConversationType.Direct).Select(c => c.Id).ToHashSet();
        var otherUserIdByConversation = conversations
            .Where(c => directConversationIds.Contains(c.Id))
            .ToDictionary(c => c.Id, c => c.DirectUserAId == currentUserId ? c.DirectUserBId : c.DirectUserAId);
        var otherUserIds = otherUserIdByConversation.Values.Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();

        var profiles = await dbContext.UserProfiles.AsNoTracking()
            .Where(p => otherUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var profileMap = profiles.ToDictionary(p => p.UserId);

        var lastMessages = await dbContext.ChatMessages.AsNoTracking()
            .Where(m => conversationIds.Contains(m.ConversationId))
            .GroupBy(m => m.ConversationId)
            .Select(g => g.OrderByDescending(m => m.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var lastMessageMap = lastMessages.ToDictionary(m => m.ConversationId);

        var allParticipantRows = await dbContext.ConversationParticipants.AsNoTracking()
            .Where(p => conversationIds.Contains(p.ConversationId))
            .ToListAsync(cancellationToken);
        var allParticipantUserIds = allParticipantRows.Select(p => p.UserId).Distinct().ToList();
        var allProfiles = await dbContext.UserProfiles.AsNoTracking()
            .Where(p => allParticipantUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var allProfileMap = allProfiles.ToDictionary(p => p.UserId);
        var participantsByConversation = allParticipantRows
            .GroupBy(p => p.ConversationId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ChatParticipantDto>)g
                    .Where(p => allProfileMap.ContainsKey(p.UserId))
                    .Select(p =>
                    {
                        var profile = allProfileMap[p.UserId];
                        return new ChatParticipantDto(profile.UserId, profile.Username, profile.DisplayName, profile.AvatarUrl);
                    })
                    .ToList());

        return conversations.Select(conversation =>
        {
            ChatParticipantDto? otherParticipant = null;
            if (conversation.Type == ConversationType.Direct
                && otherUserIdByConversation.TryGetValue(conversation.Id, out var otherUserId)
                && otherUserId is Guid otherId
                && profileMap.TryGetValue(otherId, out var profile))
            {
                otherParticipant = new ChatParticipantDto(profile.UserId, profile.Username, profile.DisplayName, profile.AvatarUrl);
            }

            lastMessageMap.TryGetValue(conversation.Id, out var lastMessage);
            participantsByConversation.TryGetValue(conversation.Id, out var participants);
            return new ConversationDto(
                conversation.Id,
                conversation.Type,
                conversation.Title,
                otherParticipant,
                lastMessage?.Body,
                lastMessage?.CreatedAt,
                conversation.CreatedAt,
                conversation.UpdatedAt,
                participants ?? [],
                conversation.LfgGroupId);
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
                message.MediaUrl,
                message.MediaType,
                message.CreatedAt,
                message.ReadAt);
        }).ToList();
    }

    private static (Guid UserAId, Guid UserBId) OrderPair(Guid userId1, Guid userId2) =>
        userId1.CompareTo(userId2) < 0 ? (userId1, userId2) : (userId2, userId1);
}
