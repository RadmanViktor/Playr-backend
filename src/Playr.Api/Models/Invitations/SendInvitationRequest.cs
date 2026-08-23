namespace Playr.Api.Models.Invitations;

public sealed record SendInvitationRequest(Guid RecipientUserId, string Message);
