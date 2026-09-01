using Microsoft.EntityFrameworkCore;
using Playr.Application.Chat;
using Playr.Application.Lfg;
using Playr.Application.Notifications;
using Playr.Application.Profiles;
using Playr.Domain.Games;
using Playr.Domain.Identity;
using Playr.Domain.Lfg;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;

namespace Playr.Infrastructure.Lfg;

public sealed class LfgGroupService(
    PlayrDbContext dbContext,
    IChatService chatService,
    ILfgGroupNotifier lfgGroupNotifier,
    INotificationFeedService notificationFeedService,
    IProfileService profileService) : ILfgGroupService
{
    private const int MaxNoteLength = 200;
    private const int MaxPlayersWanted = 10;

    public async Task<LfgGroupDto> CreateGroupAsync(Guid creatorUserId, CreateLfgGroupCommand command, CancellationToken cancellationToken)
    {
        if (command.PlayersWanted < 1 || command.PlayersWanted > MaxPlayersWanted)
        {
            throw new InvalidOperationException($"Players wanted must be between 1 and {MaxPlayersWanted}.");
        }

        var gameExists = await dbContext.Games.AsNoTracking().AnyAsync(g => g.Id == command.GameId, cancellationToken);
        if (!gameExists)
        {
            throw new InvalidOperationException("The selected game was not found.");
        }

        var hasOpenGroup = await dbContext.LfgGroups.AsNoTracking()
            .AnyAsync(g => g.CreatorUserId == creatorUserId && g.Status == LfgGroupStatus.Open, cancellationToken);
        if (hasOpenGroup)
        {
            throw new InvalidOperationException("You already have an open group. Cancel it before creating a new one.");
        }

        var isAvailable = await dbContext.UserProfiles.AsNoTracking()
            .AnyAsync(p => p.UserId == creatorUserId && p.Status == ProfileStatus.LookingForGame, cancellationToken);
        if (isAvailable)
        {
            throw new InvalidOperationException("You are currently set as available. Turn that off before creating a group.");
        }

        var note = NormalizeNote(command.Note);
        var now = DateTimeOffset.UtcNow;

        var group = new LfgGroup
        {
            Id = Guid.NewGuid(),
            CreatorUserId = creatorUserId,
            GameId = command.GameId,
            PlayStyle = command.PlayStyle,
            Note = note,
            PlayersWanted = command.PlayersWanted,
            Status = LfgGroupStatus.Open,
            CreatedAt = now,
            Members =
            [
                new LfgGroupMember { UserId = creatorUserId, JoinedAt = now, IsCreator = true }
            ]
        };

        dbContext.LfgGroups.Add(group);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = await LoadGroupDtoAsync(group.Id, creatorUserId, cancellationToken);
        await lfgGroupNotifier.NotifyGroupUpdatedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<IReadOnlyList<LfgGroupDto>> GetOpenGroupsAsync(Guid currentUserId, CancellationToken cancellationToken)
    {
        var groups = await dbContext.LfgGroups.AsNoTracking()
            .Where(g => g.Status == LfgGroupStatus.Open)
            .Include(g => g.Creator).ThenInclude(u => u.Profile)
            .Include(g => g.Game)
            .Include(g => g.Members)
            .Include(g => g.Applications)
            .Include(g => g.Invites)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);

        return groups.Select(g => ToDto(g, currentUserId)).ToList();
    }

    public async Task<LfgGroupApplicationDto> ApplyAsync(Guid applicantUserId, Guid groupId, string? message, CancellationToken cancellationToken)
    {
        var group = await dbContext.LfgGroups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new InvalidOperationException("Group was not found.");

        if (group.Status != LfgGroupStatus.Open)
        {
            throw new InvalidOperationException("This group is not open for applications.");
        }

        if (group.CreatorUserId == applicantUserId)
        {
            throw new InvalidOperationException("You cannot apply to your own group.");
        }

        var isMember = await dbContext.LfgGroupMembers.AsNoTracking()
            .AnyAsync(m => m.LfgGroupId == groupId && m.UserId == applicantUserId, cancellationToken);
        if (isMember)
        {
            throw new InvalidOperationException("You are already a member of this group.");
        }

        var hasPendingApplication = await dbContext.LfgGroupApplications.AsNoTracking()
            .AnyAsync(a => a.LfgGroupId == groupId && a.ApplicantUserId == applicantUserId && a.Status == LfgApplicationStatus.Pending, cancellationToken);
        if (hasPendingApplication)
        {
            throw new InvalidOperationException("You already have a pending application for this group.");
        }

        var trimmedMessage = NormalizeNote(message);
        var application = new LfgGroupApplication
        {
            Id = Guid.NewGuid(),
            LfgGroupId = groupId,
            ApplicantUserId = applicantUserId,
            Status = LfgApplicationStatus.Pending,
            Message = trimmedMessage,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.LfgGroupApplications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = await LoadApplicationDtoAsync(application.Id, cancellationToken);
        await lfgGroupNotifier.NotifyApplicationReceivedAsync(group.CreatorUserId, dto, cancellationToken);
        await notificationFeedService.CreateLfgApplicationNotificationAsync(applicantUserId, group.CreatorUserId, groupId, cancellationToken);
        return dto;
    }

    public async Task<LfgGroupApplicationDto> AcceptApplicationAsync(Guid creatorUserId, Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await dbContext.LfgGroupApplications
            .Include(a => a.LfgGroup)
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken)
            ?? throw new InvalidOperationException("Application was not found.");

        var group = application.LfgGroup;
        if (group.CreatorUserId != creatorUserId)
        {
            throw new InvalidOperationException("Only the group creator can accept applications.");
        }

        if (application.Status != LfgApplicationStatus.Pending)
        {
            throw new InvalidOperationException("This application has already been responded to.");
        }

        if (group.Status != LfgGroupStatus.Open)
        {
            throw new InvalidOperationException("This group is no longer open.");
        }

        var now = DateTimeOffset.UtcNow;
        application.Status = LfgApplicationStatus.Accepted;
        application.RespondedAt = now;

        dbContext.LfgGroupMembers.Add(new LfgGroupMember
        {
            LfgGroupId = group.Id,
            UserId = application.ApplicantUserId,
            JoinedAt = now,
            IsCreator = false
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        // The applicant has now joined a group - if they were still showing as
        // "Looking for game" themselves, reset them back to Online.
        await profileService.ClearLookingForGameStatusAsync(application.ApplicantUserId, cancellationToken);

        await CheckAndFillGroupAsync(group.Id, cancellationToken);

        var dto = await LoadApplicationDtoAsync(application.Id, cancellationToken);
        return dto;
    }

    public async Task<LfgGroupApplicationDto> DeclineApplicationAsync(Guid creatorUserId, Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await dbContext.LfgGroupApplications
            .Include(a => a.LfgGroup)
            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken)
            ?? throw new InvalidOperationException("Application was not found.");

        if (application.LfgGroup.CreatorUserId != creatorUserId)
        {
            throw new InvalidOperationException("Only the group creator can decline applications.");
        }

        if (application.Status != LfgApplicationStatus.Pending)
        {
            throw new InvalidOperationException("This application has already been responded to.");
        }

        application.Status = LfgApplicationStatus.Declined;
        application.RespondedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return await LoadApplicationDtoAsync(application.Id, cancellationToken);
    }

    public async Task<LfgGroupInviteDto> InviteToGroupAsync(Guid creatorUserId, Guid groupId, Guid inviteeUserId, CancellationToken cancellationToken)
    {
        var group = await dbContext.LfgGroups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new InvalidOperationException("Group was not found.");

        if (group.CreatorUserId != creatorUserId)
        {
            throw new InvalidOperationException("Only the group creator can invite players.");
        }

        if (group.Status != LfgGroupStatus.Open)
        {
            throw new InvalidOperationException("This group is not open for invites.");
        }

        if (inviteeUserId == creatorUserId)
        {
            throw new InvalidOperationException("You cannot invite yourself.");
        }

        var isMember = await dbContext.LfgGroupMembers.AsNoTracking()
            .AnyAsync(m => m.LfgGroupId == groupId && m.UserId == inviteeUserId, cancellationToken);
        if (isMember)
        {
            throw new InvalidOperationException("This player is already a member of the group.");
        }

        var hasPendingInvite = await dbContext.LfgGroupInvites.AsNoTracking()
            .AnyAsync(i => i.LfgGroupId == groupId && i.InviteeUserId == inviteeUserId && i.Status == LfgInviteStatus.Pending, cancellationToken);
        if (hasPendingInvite)
        {
            throw new InvalidOperationException("This player already has a pending invite for this group.");
        }

        var inviteeExists = await dbContext.UserProfiles.AsNoTracking().AnyAsync(p => p.UserId == inviteeUserId, cancellationToken);
        if (!inviteeExists)
        {
            throw new InvalidOperationException("The invited player was not found.");
        }

        var invite = new LfgGroupInvite
        {
            Id = Guid.NewGuid(),
            LfgGroupId = groupId,
            InviterUserId = creatorUserId,
            InviteeUserId = inviteeUserId,
            Status = LfgInviteStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.LfgGroupInvites.Add(invite);
        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = await LoadInviteDtoAsync(invite.Id, cancellationToken);
        await lfgGroupNotifier.NotifyGroupInviteReceivedAsync(inviteeUserId, dto, cancellationToken);
        return dto;
    }

    public async Task<LfgGroupInviteDto> RespondToGroupInviteAsync(Guid inviteeUserId, Guid inviteId, bool accept, CancellationToken cancellationToken)
    {
        var invite = await dbContext.LfgGroupInvites
            .Include(i => i.LfgGroup)
            .FirstOrDefaultAsync(i => i.Id == inviteId, cancellationToken)
            ?? throw new InvalidOperationException("Invite was not found.");

        if (invite.InviteeUserId != inviteeUserId)
        {
            throw new InvalidOperationException("Only the invitee can respond to this invite.");
        }

        if (invite.Status != LfgInviteStatus.Pending)
        {
            throw new InvalidOperationException("This invite has already been responded to.");
        }

        var now = DateTimeOffset.UtcNow;
        if (!accept)
        {
            invite.Status = LfgInviteStatus.Declined;
            invite.RespondedAt = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return await LoadInviteDtoAsync(invite.Id, cancellationToken);
        }

        if (invite.LfgGroup.Status != LfgGroupStatus.Open)
        {
            throw new InvalidOperationException("This group is no longer open.");
        }

        invite.Status = LfgInviteStatus.Accepted;
        invite.RespondedAt = now;

        dbContext.LfgGroupMembers.Add(new LfgGroupMember
        {
            LfgGroupId = invite.LfgGroupId,
            UserId = inviteeUserId,
            JoinedAt = now,
            IsCreator = false
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        // The invitee has now joined a group - if they were still showing as
        // "Looking for game" themselves, reset them back to Online.
        await profileService.ClearLookingForGameStatusAsync(inviteeUserId, cancellationToken);

        await CheckAndFillGroupAsync(invite.LfgGroupId, cancellationToken);

        return await LoadInviteDtoAsync(invite.Id, cancellationToken);
    }

    public async Task<LfgGroupDto> CancelGroupAsync(Guid creatorUserId, Guid groupId, CancellationToken cancellationToken)
    {
        var group = await dbContext.LfgGroups.FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken)
            ?? throw new InvalidOperationException("Group was not found.");

        if (group.CreatorUserId != creatorUserId)
        {
            throw new InvalidOperationException("Only the group creator can cancel this group.");
        }

        if (group.Status != LfgGroupStatus.Open)
        {
            throw new InvalidOperationException("Only open groups can be cancelled.");
        }

        var now = DateTimeOffset.UtcNow;
        group.Status = LfgGroupStatus.Cancelled;
        group.CancelledAt = now;

        await DeclinePendingApplicationsAndInvitesAsync(groupId, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var dto = await LoadGroupDtoAsync(groupId, creatorUserId, cancellationToken);
        await lfgGroupNotifier.NotifyGroupUpdatedAsync(dto, cancellationToken);
        return dto;
    }

    public async Task<IReadOnlyList<LfgGroupApplicationDto>> GetIncomingApplicationsAsync(Guid creatorUserId, CancellationToken cancellationToken)
    {
        var applications = await dbContext.LfgGroupApplications.AsNoTracking()
            .Where(a => a.LfgGroup.CreatorUserId == creatorUserId && a.Status == LfgApplicationStatus.Pending)
            .Include(a => a.Applicant).ThenInclude(u => u.Profile)
            .Include(a => a.LfgGroup).ThenInclude(g => g.Game)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return applications.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<LfgGroupInviteDto>> GetMyGroupInvitesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var invites = await dbContext.LfgGroupInvites.AsNoTracking()
            .Where(i => i.InviteeUserId == userId && i.Status == LfgInviteStatus.Pending)
            .Include(i => i.Invitee).ThenInclude(u => u.Profile)
            .Include(i => i.LfgGroup).ThenInclude(g => g.Game)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        return invites.Select(ToDto).ToList();
    }

    private async Task CheckAndFillGroupAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await dbContext.LfgGroups
            .Include(g => g.Members)
            .FirstAsync(g => g.Id == groupId, cancellationToken);

        if (group.Status != LfgGroupStatus.Open)
        {
            return;
        }

        var acceptedCount = group.Members.Count - 1;
        if (acceptedCount < group.PlayersWanted)
        {
            // Not full yet - still broadcast the updated accepted-count so everyone
            // browsing the open groups list (and the creator's incoming-applications
            // view) sees the counter tick up live instead of waiting for a refresh.
            var partialDto = await LoadGroupDtoAsync(groupId, group.CreatorUserId, cancellationToken);
            await lfgGroupNotifier.NotifyGroupUpdatedAsync(partialDto, cancellationToken);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        group.Status = LfgGroupStatus.Filled;
        group.FilledAt = now;

        await DeclinePendingApplicationsAndInvitesAsync(groupId, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var memberUserIds = group.Members.Select(m => m.UserId).ToList();

        var game = await dbContext.Games.AsNoTracking().FirstAsync(g => g.Id == group.GameId, cancellationToken);
        await chatService.GetOrCreateGroupConversationAsync(memberUserIds, $"{game.Name} gäng", group.Id, cancellationToken);

        var dto = await LoadGroupDtoAsync(groupId, group.CreatorUserId, cancellationToken);
        await lfgGroupNotifier.NotifyGroupFilledAsync(memberUserIds, dto, cancellationToken);
    }

    private async Task DeclinePendingApplicationsAndInvitesAsync(Guid groupId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pendingApplications = await dbContext.LfgGroupApplications
            .Where(a => a.LfgGroupId == groupId && a.Status == LfgApplicationStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var application in pendingApplications)
        {
            application.Status = LfgApplicationStatus.Declined;
            application.RespondedAt = now;
        }

        var pendingInvites = await dbContext.LfgGroupInvites
            .Where(i => i.LfgGroupId == groupId && i.Status == LfgInviteStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var invite in pendingInvites)
        {
            invite.Status = LfgInviteStatus.Cancelled;
            invite.RespondedAt = now;
        }
    }

    private async Task<LfgGroupDto> LoadGroupDtoAsync(Guid groupId, Guid currentUserId, CancellationToken cancellationToken)
    {
        var group = await dbContext.LfgGroups.AsNoTracking()
            .Include(g => g.Creator).ThenInclude(u => u.Profile)
            .Include(g => g.Game)
            .Include(g => g.Members)
            .Include(g => g.Applications)
            .Include(g => g.Invites)
            .FirstAsync(g => g.Id == groupId, cancellationToken);

        return ToDto(group, currentUserId);
    }

    private async Task<LfgGroupApplicationDto> LoadApplicationDtoAsync(Guid applicationId, CancellationToken cancellationToken)
    {
        var application = await dbContext.LfgGroupApplications.AsNoTracking()
            .Include(a => a.Applicant).ThenInclude(u => u.Profile)
            .Include(a => a.LfgGroup).ThenInclude(g => g.Game)
            .FirstAsync(a => a.Id == applicationId, cancellationToken);

        return ToDto(application);
    }

    private async Task<LfgGroupInviteDto> LoadInviteDtoAsync(Guid inviteId, CancellationToken cancellationToken)
    {
        var invite = await dbContext.LfgGroupInvites.AsNoTracking()
            .Include(i => i.Invitee).ThenInclude(u => u.Profile)
            .Include(i => i.LfgGroup).ThenInclude(g => g.Game)
            .FirstAsync(i => i.Id == inviteId, cancellationToken);

        return ToDto(invite);
    }

    private static LfgGroupDto ToDto(LfgGroup group, Guid currentUserId)
    {
        var myMembership = LfgMyMembershipStatus.None;
        if (group.CreatorUserId == currentUserId)
        {
            myMembership = LfgMyMembershipStatus.IsCreator;
        }
        else if (group.Members.Any(m => m.UserId == currentUserId))
        {
            myMembership = LfgMyMembershipStatus.IsMember;
        }

        var myApplication = group.Applications
            .Where(a => a.ApplicantUserId == currentUserId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();
        var myInvite = group.Invites
            .Where(i => i.InviteeUserId == currentUserId)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefault();

        return new LfgGroupDto(
            group.Id,
            group.CreatorUserId,
            group.Creator.Profile!.Username,
            group.Creator.Profile!.DisplayName,
            group.Creator.Profile!.AvatarUrl,
            group.GameId,
            group.Game.Name,
            group.Game.CoverImageUrl,
            group.PlayStyle,
            group.Note,
            group.PlayersWanted,
            group.Members.Count - 1,
            group.Status,
            group.CreatedAt,
            group.FilledAt,
            group.CancelledAt,
            myMembership,
            myApplication?.Status,
            myInvite?.Status);
    }

    private static LfgGroupApplicationDto ToDto(LfgGroupApplication application) => new(
        application.Id,
        application.LfgGroupId,
        application.LfgGroup.Game.Name,
        application.ApplicantUserId,
        application.Applicant.Profile!.Username,
        application.Applicant.Profile!.DisplayName,
        application.Applicant.Profile!.AvatarUrl,
        application.Status,
        application.Message,
        application.CreatedAt,
        application.RespondedAt);

    private static LfgGroupInviteDto ToDto(LfgGroupInvite invite) => new(
        invite.Id,
        invite.LfgGroupId,
        invite.LfgGroup.Game.Name,
        invite.InviterUserId,
        invite.InviteeUserId,
        invite.Invitee.Profile!.Username,
        invite.Invitee.Profile!.DisplayName,
        invite.Invitee.Profile!.AvatarUrl,
        invite.Status,
        invite.CreatedAt,
        invite.RespondedAt);

    private static string? NormalizeNote(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (trimmed.Length > MaxNoteLength)
        {
            throw new InvalidOperationException($"Note cannot be longer than {MaxNoteLength} characters.");
        }

        return trimmed;
    }
}
