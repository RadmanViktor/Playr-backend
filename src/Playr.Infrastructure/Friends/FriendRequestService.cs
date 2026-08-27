using Microsoft.EntityFrameworkCore;
using Playr.Application.Friends;
using Playr.Domain.Friendships;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Friends;

public sealed class FriendRequestService(PlayrDbContext dbContext) : IFriendRequestService
{
    public async Task<FriendRequestDto> SendAsync(Guid senderUserId, SendFriendRequestCommand command, CancellationToken cancellationToken)
    {
        if (command.RecipientUserId == senderUserId)
        {
            throw new InvalidOperationException("You cannot send a friend request to yourself.");
        }

        var recipientExists = await dbContext.UserProfiles.AsNoTracking()
            .AnyAsync(p => p.UserId == command.RecipientUserId, cancellationToken);
        if (!recipientExists)
        {
            throw new InvalidOperationException("Recipient was not found.");
        }

        if (await AreFriendsAsync(senderUserId, command.RecipientUserId, cancellationToken))
        {
            throw new InvalidOperationException("You are already friends with this player.");
        }

        var hasPendingRequest = await dbContext.FriendRequests.AsNoTracking().AnyAsync(r =>
            r.Status == FriendRequestStatus.Pending &&
            ((r.SenderUserId == senderUserId && r.RecipientUserId == command.RecipientUserId) ||
             (r.SenderUserId == command.RecipientUserId && r.RecipientUserId == senderUserId)),
            cancellationToken);
        if (hasPendingRequest)
        {
            throw new InvalidOperationException("There is already a pending friend request between you and this player.");
        }

        var friendRequest = new FriendRequest
        {
            Id = Guid.NewGuid(),
            SenderUserId = senderUserId,
            RecipientUserId = command.RecipientUserId,
            Status = FriendRequestStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.FriendRequests.Add(friendRequest);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadDtoAsync(friendRequest.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<FriendRequestDto>> GetIncomingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var requests = await dbContext.FriendRequests.AsNoTracking()
            .Where(r => r.RecipientUserId == userId && r.Status == FriendRequestStatus.Pending)
            .Include(r => r.Sender).ThenInclude(u => u.Profile)
            .Include(r => r.Recipient).ThenInclude(u => u.Profile)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
        return requests.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<FriendRequestDto>> GetSentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var requests = await dbContext.FriendRequests.AsNoTracking()
            .Where(r => r.SenderUserId == userId)
            .Include(r => r.Sender).ThenInclude(u => u.Profile)
            .Include(r => r.Recipient).ThenInclude(u => u.Profile)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);
        return requests.Select(ToDto).ToList();
    }

    public async Task<FriendRequestDto> AcceptAsync(Guid userId, Guid friendRequestId, CancellationToken cancellationToken)
    {
        var request = await dbContext.FriendRequests
            .FirstOrDefaultAsync(r => r.Id == friendRequestId, cancellationToken)
            ?? throw new InvalidOperationException("Friend request was not found.");

        if (request.RecipientUserId != userId)
        {
            throw new InvalidOperationException("Only the recipient can accept this friend request.");
        }

        if (request.Status != FriendRequestStatus.Pending)
        {
            throw new InvalidOperationException("This friend request has already been responded to.");
        }

        request.Status = FriendRequestStatus.Accepted;
        request.RespondedAt = DateTimeOffset.UtcNow;

        if (!await AreFriendsAsync(request.SenderUserId, request.RecipientUserId, cancellationToken))
        {
            var (userAId, userBId) = OrderPair(request.SenderUserId, request.RecipientUserId);
            dbContext.Friendships.Add(new Friendship
            {
                Id = Guid.NewGuid(),
                UserAId = userAId,
                UserBId = userBId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await LoadDtoAsync(request.Id, cancellationToken);
    }

    public async Task<FriendRequestDto> DeclineAsync(Guid userId, Guid friendRequestId, CancellationToken cancellationToken)
    {
        var request = await dbContext.FriendRequests
            .FirstOrDefaultAsync(r => r.Id == friendRequestId, cancellationToken)
            ?? throw new InvalidOperationException("Friend request was not found.");

        if (request.RecipientUserId != userId)
        {
            throw new InvalidOperationException("Only the recipient can decline this friend request.");
        }

        if (request.Status != FriendRequestStatus.Pending)
        {
            throw new InvalidOperationException("This friend request has already been responded to.");
        }

        request.Status = FriendRequestStatus.Declined;
        request.RespondedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await LoadDtoAsync(request.Id, cancellationToken);
    }

    public async Task<FriendRequestDto> CancelAsync(Guid userId, Guid friendRequestId, CancellationToken cancellationToken)
    {
        var request = await dbContext.FriendRequests
            .FirstOrDefaultAsync(r => r.Id == friendRequestId, cancellationToken)
            ?? throw new InvalidOperationException("Friend request was not found.");

        if (request.SenderUserId != userId)
        {
            throw new InvalidOperationException("Only the sender can cancel this friend request.");
        }

        if (request.Status != FriendRequestStatus.Pending)
        {
            throw new InvalidOperationException("This friend request has already been responded to.");
        }

        request.Status = FriendRequestStatus.Cancelled;
        request.RespondedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await LoadDtoAsync(request.Id, cancellationToken);
    }

    private async Task<bool> AreFriendsAsync(Guid userId1, Guid userId2, CancellationToken cancellationToken)
    {
        var (userAId, userBId) = OrderPair(userId1, userId2);
        return await dbContext.Friendships.AsNoTracking()
            .AnyAsync(f => f.UserAId == userAId && f.UserBId == userBId, cancellationToken);
    }

    private static (Guid UserAId, Guid UserBId) OrderPair(Guid userId1, Guid userId2) =>
        userId1.CompareTo(userId2) < 0 ? (userId1, userId2) : (userId2, userId1);

    private async Task<FriendRequestDto> LoadDtoAsync(Guid friendRequestId, CancellationToken cancellationToken)
    {
        var request = await dbContext.FriendRequests.AsNoTracking()
            .Include(r => r.Sender).ThenInclude(u => u.Profile)
            .Include(r => r.Recipient).ThenInclude(u => u.Profile)
            .FirstAsync(r => r.Id == friendRequestId, cancellationToken);
        return ToDto(request);
    }

    private static FriendRequestDto ToDto(FriendRequest request) => new(
        request.Id,
        request.SenderUserId,
        request.Sender.Profile!.Username,
        request.Sender.Profile!.DisplayName,
        request.Sender.Profile!.AvatarUrl,
        request.RecipientUserId,
        request.Recipient.Profile!.Username,
        request.Recipient.Profile!.DisplayName,
        request.Recipient.Profile!.AvatarUrl,
        request.Status,
        request.CreatedAt,
        request.RespondedAt);
}
