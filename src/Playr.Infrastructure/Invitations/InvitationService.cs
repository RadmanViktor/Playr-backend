using Microsoft.EntityFrameworkCore;
using Playr.Application.Invitations;
using Playr.Domain.Friendships;
using Playr.Domain.Invitations;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Invitations;

public sealed class InvitationService(PlayrDbContext dbContext) : IInvitationService
{
    private const int MaxMessageLength = 500;

    public async Task<InvitationDto> SendAsync(Guid senderUserId, SendInvitationCommand command, CancellationToken cancellationToken)
    {
        if (command.RecipientUserId == senderUserId)
        {
            throw new InvalidOperationException("You cannot invite yourself.");
        }

        var message = (command.Message ?? string.Empty).Trim();
        if (message.Length == 0)
        {
            throw new InvalidOperationException("A message is required.");
        }

        if (message.Length > MaxMessageLength)
        {
            throw new InvalidOperationException($"Message cannot be longer than {MaxMessageLength} characters.");
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

        var hasPendingInvitation = await dbContext.Invitations.AsNoTracking().AnyAsync(i =>
            i.Status == InvitationStatus.Pending &&
            ((i.SenderUserId == senderUserId && i.RecipientUserId == command.RecipientUserId) ||
             (i.SenderUserId == command.RecipientUserId && i.RecipientUserId == senderUserId)),
            cancellationToken);
        if (hasPendingInvitation)
        {
            throw new InvalidOperationException("There is already a pending invitation between you and this player.");
        }

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            SenderUserId = senderUserId,
            RecipientUserId = command.RecipientUserId,
            Message = message,
            Status = InvitationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadDtoAsync(invitation.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<InvitationDto>> GetIncomingAsync(Guid userId, CancellationToken cancellationToken)
    {
        var invitations = await dbContext.Invitations.AsNoTracking()
            .Where(i => i.RecipientUserId == userId && i.Status == InvitationStatus.Pending)
            .Include(i => i.Sender).ThenInclude(u => u.Profile)
            .Include(i => i.Recipient).ThenInclude(u => u.Profile)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
        return invitations.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<InvitationDto>> GetSentAsync(Guid userId, CancellationToken cancellationToken)
    {
        var invitations = await dbContext.Invitations.AsNoTracking()
            .Where(i => i.SenderUserId == userId)
            .Include(i => i.Sender).ThenInclude(u => u.Profile)
            .Include(i => i.Recipient).ThenInclude(u => u.Profile)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
        return invitations.Select(ToDto).ToList();
    }

    public async Task<InvitationDto> AcceptAsync(Guid userId, Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await dbContext.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken)
            ?? throw new InvalidOperationException("Invitation was not found.");

        if (invitation.RecipientUserId != userId)
        {
            throw new InvalidOperationException("Only the recipient can accept this invitation.");
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException("This invitation has already been responded to.");
        }

        invitation.Status = InvitationStatus.Accepted;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        if (!await AreFriendsAsync(invitation.SenderUserId, invitation.RecipientUserId, cancellationToken))
        {
            var (userAId, userBId) = OrderPair(invitation.SenderUserId, invitation.RecipientUserId);
            dbContext.Friendships.Add(new Friendship
            {
                Id = Guid.NewGuid(),
                UserAId = userAId,
                UserBId = userBId,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await LoadDtoAsync(invitation.Id, cancellationToken);
    }

    public async Task<InvitationDto> DeclineAsync(Guid userId, Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await dbContext.Invitations
            .FirstOrDefaultAsync(i => i.Id == invitationId, cancellationToken)
            ?? throw new InvalidOperationException("Invitation was not found.");

        if (invitation.RecipientUserId != userId)
        {
            throw new InvalidOperationException("Only the recipient can decline this invitation.");
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            throw new InvalidOperationException("This invitation has already been responded to.");
        }

        invitation.Status = InvitationStatus.Declined;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await LoadDtoAsync(invitation.Id, cancellationToken);
    }

    private async Task<bool> AreFriendsAsync(Guid userId1, Guid userId2, CancellationToken cancellationToken)
    {
        var (userAId, userBId) = OrderPair(userId1, userId2);
        return await dbContext.Friendships.AsNoTracking()
            .AnyAsync(f => f.UserAId == userAId && f.UserBId == userBId, cancellationToken);
    }

    private static (Guid UserAId, Guid UserBId) OrderPair(Guid userId1, Guid userId2) =>
        userId1.CompareTo(userId2) < 0 ? (userId1, userId2) : (userId2, userId1);

    private async Task<InvitationDto> LoadDtoAsync(Guid invitationId, CancellationToken cancellationToken)
    {
        var invitation = await dbContext.Invitations.AsNoTracking()
            .Include(i => i.Sender).ThenInclude(u => u.Profile)
            .Include(i => i.Recipient).ThenInclude(u => u.Profile)
            .FirstAsync(i => i.Id == invitationId, cancellationToken);
        return ToDto(invitation);
    }

    private static InvitationDto ToDto(Domain.Invitations.Invitation invitation) => new(
        invitation.Id,
        invitation.SenderUserId,
        invitation.Sender.Profile!.Username,
        invitation.Sender.Profile!.DisplayName,
        invitation.Sender.Profile!.AvatarUrl,
        invitation.RecipientUserId,
        invitation.Recipient.Profile!.Username,
        invitation.Recipient.Profile!.DisplayName,
        invitation.Recipient.Profile!.AvatarUrl,
        invitation.Message,
        invitation.Status,
        invitation.CreatedAt,
        invitation.RespondedAt);
}
