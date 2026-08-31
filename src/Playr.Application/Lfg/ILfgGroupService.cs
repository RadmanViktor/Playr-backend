namespace Playr.Application.Lfg;

public interface ILfgGroupService
{
    Task<LfgGroupDto> CreateGroupAsync(Guid creatorUserId, CreateLfgGroupCommand command, CancellationToken cancellationToken);
    Task<IReadOnlyList<LfgGroupDto>> GetOpenGroupsAsync(Guid currentUserId, CancellationToken cancellationToken);
    Task<LfgGroupApplicationDto> ApplyAsync(Guid applicantUserId, Guid groupId, string? message, CancellationToken cancellationToken);
    Task<LfgGroupApplicationDto> AcceptApplicationAsync(Guid creatorUserId, Guid applicationId, CancellationToken cancellationToken);
    Task<LfgGroupApplicationDto> DeclineApplicationAsync(Guid creatorUserId, Guid applicationId, CancellationToken cancellationToken);
    Task<LfgGroupInviteDto> InviteToGroupAsync(Guid creatorUserId, Guid groupId, Guid inviteeUserId, CancellationToken cancellationToken);
    Task<LfgGroupInviteDto> RespondToGroupInviteAsync(Guid inviteeUserId, Guid inviteId, bool accept, CancellationToken cancellationToken);
    Task<LfgGroupDto> CancelGroupAsync(Guid creatorUserId, Guid groupId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LfgGroupApplicationDto>> GetIncomingApplicationsAsync(Guid creatorUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<LfgGroupInviteDto>> GetMyGroupInvitesAsync(Guid userId, CancellationToken cancellationToken);
}
