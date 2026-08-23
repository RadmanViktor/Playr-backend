namespace Playr.Application.Invitations;

public sealed record SendInvitationCommand(Guid RecipientUserId, string Message);
