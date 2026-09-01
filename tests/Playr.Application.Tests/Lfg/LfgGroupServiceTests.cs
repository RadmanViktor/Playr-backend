using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Chat;
using Playr.Application.Lfg;
using Playr.Application.Tests.Chat;
using Playr.Application.Tests.Notifications;
using Playr.Application.Tests.Posts;
using Playr.Domain.Identity;
using Playr.Domain.Lfg;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Chat;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Lfg;

namespace Playr.Application.Tests.Lfg;

public sealed class LfgGroupServiceTests
{
    private static readonly Guid GameId = Guid.Parse("00000001-0000-0000-0000-000000000001");

    [Fact]
    public async Task CreateGroupAsync_CreatesGroupWithCreatorAsMember()
    {
        await using var fixture = await LfgFixture.CreateAsync();

        var group = await fixture.Service.CreateGroupAsync(
            fixture.CreatorUserId,
            new CreateLfgGroupCommand(GameId, 2, PlayStyle.Chill, "Let's have fun"),
            CancellationToken.None);

        group.Status.Should().Be(LfgGroupStatus.Open);
        group.MyMembershipStatus.Should().Be(LfgMyMembershipStatus.IsCreator);
        group.AcceptedCount.Should().Be(0);
        fixture.DbContext.LfgGroupMembers.Should().ContainSingle(m => m.UserId == fixture.CreatorUserId && m.IsCreator);
    }

    [Fact]
    public async Task CreateGroupAsync_WhenUserAlreadyHasOpenGroup_Throws()
    {
        await using var fixture = await LfgFixture.CreateAsync();
        await fixture.Service.CreateGroupAsync(
            fixture.CreatorUserId,
            new CreateLfgGroupCommand(GameId, 2, PlayStyle.Chill, null),
            CancellationToken.None);

        var act = () => fixture.Service.CreateGroupAsync(
            fixture.CreatorUserId,
            new CreateLfgGroupCommand(GameId, 1, PlayStyle.Competitive, null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("You already have an open group. Cancel it before creating a new one.");
    }

    [Fact]
    public async Task ApplyAsync_CreatesApplication()
    {
        await using var fixture = await LfgFixture.CreateAsync();
        var group = await fixture.CreateGroupAsync(playersWanted: 2);

        var application = await fixture.Service.ApplyAsync(fixture.ApplicantUserId, group.Id, "Let me in!", CancellationToken.None);

        application.Status.Should().Be(LfgApplicationStatus.Pending);
        application.ApplicantUserId.Should().Be(fixture.ApplicantUserId);
    }

    [Fact]
    public async Task AcceptApplicationAsync_AddsMemberAndIncrementsAcceptedCount()
    {
        await using var fixture = await LfgFixture.CreateAsync();
        var group = await fixture.CreateGroupAsync(playersWanted: 2);
        var application = await fixture.Service.ApplyAsync(fixture.ApplicantUserId, group.Id, null, CancellationToken.None);

        await fixture.Service.AcceptApplicationAsync(fixture.CreatorUserId, application.Id, CancellationToken.None);

        fixture.DbContext.LfgGroupMembers.Should().ContainSingle(m => m.UserId == fixture.ApplicantUserId);
        var reloaded = (await fixture.Service.GetOpenGroupsAsync(fixture.CreatorUserId, CancellationToken.None)).Single();
        reloaded.AcceptedCount.Should().Be(1);
        reloaded.Status.Should().Be(LfgGroupStatus.Open);
    }

    [Fact]
    public async Task AcceptApplicationAsync_WhenGroupBecomesFull_SetsFilledStatusAndCreatesGroupConversation()
    {
        await using var fixture = await LfgFixture.CreateAsync();
        var group = await fixture.CreateGroupAsync(playersWanted: 1);
        var application = await fixture.Service.ApplyAsync(fixture.ApplicantUserId, group.Id, null, CancellationToken.None);

        await fixture.Service.AcceptApplicationAsync(fixture.CreatorUserId, application.Id, CancellationToken.None);

        var dbGroup = await fixture.DbContext.LfgGroups.AsNoTracking().FirstAsync(g => g.Id == group.Id);
        dbGroup.Status.Should().Be(LfgGroupStatus.Filled);
        dbGroup.FilledAt.Should().NotBeNull();

        fixture.DbContext.Conversations.Should().ContainSingle(c => c.LfgGroupId == group.Id);
        var conversation = fixture.DbContext.Conversations.Single(c => c.LfgGroupId == group.Id);
        fixture.DbContext.ConversationParticipants.Where(p => p.ConversationId == conversation.Id).Should().HaveCount(2);
    }

    [Fact]
    public async Task DeclineApplicationAsync_FreesSlotForNewApplicants()
    {
        await using var fixture = await LfgFixture.CreateAsync();
        var group = await fixture.CreateGroupAsync(playersWanted: 1);
        var application = await fixture.Service.ApplyAsync(fixture.ApplicantUserId, group.Id, null, CancellationToken.None);

        await fixture.Service.DeclineApplicationAsync(fixture.CreatorUserId, application.Id, CancellationToken.None);

        var reloaded = (await fixture.Service.GetOpenGroupsAsync(fixture.CreatorUserId, CancellationToken.None)).Single();
        reloaded.Status.Should().Be(LfgGroupStatus.Open);
        reloaded.AcceptedCount.Should().Be(0);

        var secondApplication = await fixture.Service.ApplyAsync(fixture.OtherUserId, group.Id, null, CancellationToken.None);
        secondApplication.Status.Should().Be(LfgApplicationStatus.Pending);
    }

    [Fact]
    public async Task InviteToGroupAsync_CreatesInvite()
    {
        await using var fixture = await LfgFixture.CreateAsync();
        var group = await fixture.CreateGroupAsync(playersWanted: 2);

        var invite = await fixture.Service.InviteToGroupAsync(fixture.CreatorUserId, group.Id, fixture.ApplicantUserId, CancellationToken.None);

        invite.Status.Should().Be(LfgInviteStatus.Pending);
        invite.InviteeUserId.Should().Be(fixture.ApplicantUserId);
    }

    [Fact]
    public async Task RespondToGroupInviteAsync_Accept_AddsMember()
    {
        await using var fixture = await LfgFixture.CreateAsync();
        var group = await fixture.CreateGroupAsync(playersWanted: 2);
        var invite = await fixture.Service.InviteToGroupAsync(fixture.CreatorUserId, group.Id, fixture.ApplicantUserId, CancellationToken.None);

        var responded = await fixture.Service.RespondToGroupInviteAsync(fixture.ApplicantUserId, invite.Id, true, CancellationToken.None);

        responded.Status.Should().Be(LfgInviteStatus.Accepted);
        fixture.DbContext.LfgGroupMembers.Should().ContainSingle(m => m.UserId == fixture.ApplicantUserId);
    }

    [Fact]
    public async Task CancelGroupAsync_SetsCancelledAndDeclinesPendingApplications()
    {
        await using var fixture = await LfgFixture.CreateAsync();
        var group = await fixture.CreateGroupAsync(playersWanted: 2);
        var application = await fixture.Service.ApplyAsync(fixture.ApplicantUserId, group.Id, null, CancellationToken.None);

        var cancelled = await fixture.Service.CancelGroupAsync(fixture.CreatorUserId, group.Id, CancellationToken.None);

        cancelled.Status.Should().Be(LfgGroupStatus.Cancelled);
        var reloadedApplication = await fixture.DbContext.LfgGroupApplications.AsNoTracking().FirstAsync(a => a.Id == application.Id);
        reloadedApplication.Status.Should().Be(LfgApplicationStatus.Declined);
    }

    [Fact]
    public async Task SendMessageAsync_ForGroupConversation_WorksForNonDirectParticipant()
    {
        await using var fixture = await LfgFixture.CreateAsync();
        var group = await fixture.CreateGroupAsync(playersWanted: 1);
        var application = await fixture.Service.ApplyAsync(fixture.ApplicantUserId, group.Id, null, CancellationToken.None);
        await fixture.Service.AcceptApplicationAsync(fixture.CreatorUserId, application.Id, CancellationToken.None);

        var conversation = fixture.DbContext.Conversations.Single(c => c.LfgGroupId == group.Id);

        var message = await fixture.ChatService.SendMessageAsync(
            fixture.ApplicantUserId,
            conversation.Id,
            new SendChatMessageCommand("gg let's play", null),
            CancellationToken.None);

        message.Body.Should().Be("gg let's play");
        var stored = await fixture.DbContext.ChatMessages.AsNoTracking().ToListAsync();
        stored.Should().ContainSingle(m => m.Body == "gg let's play");
    }

    private sealed class LfgFixture : IAsyncDisposable
    {
        private LfgFixture(SqliteConnection connection, PlayrDbContext dbContext, ChatService chatService, LfgGroupService service)
        {
            Connection = connection;
            DbContext = dbContext;
            ChatService = chatService;
            Service = service;
        }

        public SqliteConnection Connection { get; }
        public PlayrDbContext DbContext { get; }
        public ChatService ChatService { get; }
        public LfgGroupService Service { get; }
        public Guid CreatorUserId { get; } = Guid.Parse("50000000-0000-0000-0000-000000000001");
        public Guid ApplicantUserId { get; } = Guid.Parse("50000000-0000-0000-0000-000000000002");
        public Guid OtherUserId { get; } = Guid.Parse("50000000-0000-0000-0000-000000000003");

        public static async Task<LfgFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PlayrDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new PlayrDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            var chatService = new ChatService(dbContext, new NoOpChatNotifier(), new NoOpFileStorageService(), new Playr.Application.Tests.Badges.NoOpBadgeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ChatService>.Instance);
            var service = new LfgGroupService(dbContext, chatService, new NoOpLfgGroupNotifier(), new NoOpNotificationFeedService());
            var fixture = new LfgFixture(connection, dbContext, chatService, service);
            fixture.AddUser(fixture.CreatorUserId, "creator", "Creator");
            fixture.AddUser(fixture.ApplicantUserId, "applicant", "Applicant");
            fixture.AddUser(fixture.OtherUserId, "other", "Other");
            await dbContext.SaveChangesAsync();
            return fixture;
        }

        public async Task<LfgGroupDto> CreateGroupAsync(int playersWanted) =>
            await Service.CreateGroupAsync(
                CreatorUserId,
                new CreateLfgGroupCommand(GameId, playersWanted, PlayStyle.Chill, "Looking for teammates"),
                CancellationToken.None);

        private void AddUser(Guid id, string username, string displayName)
        {
            DbContext.Users.Add(new ApplicationUser
            {
                Id = id,
                Email = $"{username}@example.com",
                UserName = username,
                NormalizedEmail = $"{username.ToUpperInvariant()}@EXAMPLE.COM",
                NormalizedUserName = username.ToUpperInvariant()
            });
            DbContext.UserProfiles.Add(new UserProfile
            {
                UserId = id,
                Username = username,
                DisplayName = displayName
            });
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
