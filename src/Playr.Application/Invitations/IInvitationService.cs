namespace Playr.Application.Invitations;

public interface IInvitationService
{
    Task<InvitationDto> SendAsync(Guid senderUserId, SendInvitationCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvitationDto>> GetIncomingAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<InvitationDto>> GetSentAsync(Guid userId, CancellationToken cancellationToken);
    Task<InvitationDto> AcceptAsync(Guid userId, Guid invitationId, CancellationToken cancellationToken);
    Task<InvitationDto> DeclineAsync(Guid userId, Guid invitationId, CancellationToken cancellationToken);
    Task<InvitationDto> CancelAsync(Guid userId, Guid invitationId, CancellationToken cancellationToken);
}
