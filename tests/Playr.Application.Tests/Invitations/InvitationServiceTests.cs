using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Invitations;
using Playr.Application.Tests.Chat;
using Playr.Application.Tests.Posts;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Chat;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Invitations;
using Playr.Infrastructure.Profiles;

namespace Playr.Application.Tests.Invitations;

public sealed class InvitationServiceTests
{
    [Fact]
    public async Task AcceptAsync_CreatesEmptyConversation()
    {
        await using var fixture = await InvitationFixture.CreateAsync();
        var sent = await fixture.Service.SendAsync(
            fixture.SenderUserId,
            new SendInvitationCommand(fixture.RecipientUserId, "Wanna play Apex tonight?"),
            CancellationToken.None);

        await fixture.Service.AcceptAsync(fixture.RecipientUserId, sent.Id, CancellationToken.None);

        fixture.DbContext.Conversations.Should().HaveCount(1);
        var messages = await fixture.DbContext.ChatMessages.AsNoTracking().ToListAsync();
        messages.Should().BeEmpty();
    }

    [Fact]
    public async Task AcceptAsync_DoesNotCreateFriendship()
    {
        await using var fixture = await InvitationFixture.CreateAsync();
        var sent = await fixture.Service.SendAsync(
            fixture.SenderUserId,
            new SendInvitationCommand(fixture.RecipientUserId, "Wanna play Apex tonight?"),
            CancellationToken.None);

        await fixture.Service.AcceptAsync(fixture.RecipientUserId, sent.Id, CancellationToken.None);

        fixture.DbContext.Friendships.Should().BeEmpty();
    }

    [Fact]
    public async Task AcceptAsync_WhenConversationExists_PreservesItsMessages()
    {
        await using var fixture = await InvitationFixture.CreateAsync();
        var conversation = await fixture.ChatService.GetOrCreateDirectConversationAsync(
            fixture.SenderUserId, fixture.RecipientUserId, CancellationToken.None);
        await fixture.ChatService.SendMessageAsync(
            fixture.SenderUserId,
            conversation.Id,
            new Playr.Application.Chat.SendChatMessageCommand("Earlier message", null),
            CancellationToken.None);
        var sent = await fixture.Service.SendAsync(
            fixture.SenderUserId,
            new SendInvitationCommand(fixture.RecipientUserId, "Play tonight?"),
            CancellationToken.None);

        await fixture.Service.AcceptAsync(fixture.RecipientUserId, sent.Id, CancellationToken.None);

        fixture.DbContext.Conversations.Should().ContainSingle();
        fixture.DbContext.ChatMessages.Should().ContainSingle(message => message.Body == "Earlier message");
    }

    [Fact]
    public async Task AcceptAsync_WhenAlreadyResponded_Throws()
    {
        await using var fixture = await InvitationFixture.CreateAsync();
        var sent = await fixture.Service.SendAsync(
            fixture.SenderUserId,
            new SendInvitationCommand(fixture.RecipientUserId, "Wanna play Apex tonight?"),
            CancellationToken.None);
        await fixture.Service.AcceptAsync(fixture.RecipientUserId, sent.Id, CancellationToken.None);

        var act = () => fixture.Service.AcceptAsync(fixture.RecipientUserId, sent.Id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("This invitation has already been responded to.");
    }

    private sealed class InvitationFixture : IAsyncDisposable
    {
        private InvitationFixture(SqliteConnection connection, PlayrDbContext dbContext, ChatService chatService, InvitationService service)
        {
            Connection = connection;
            DbContext = dbContext;
            ChatService = chatService;
            Service = service;
        }

        public SqliteConnection Connection { get; }
        public PlayrDbContext DbContext { get; }
        public ChatService ChatService { get; }
        public InvitationService Service { get; }
        public Guid SenderUserId { get; } = Guid.Parse("40000000-0000-0000-0000-000000000001");
        public Guid RecipientUserId { get; } = Guid.Parse("40000000-0000-0000-0000-000000000002");

        public static async Task<InvitationFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PlayrDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new PlayrDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            var chatService = new ChatService(dbContext, new NoOpChatNotifier(), new NoOpFileStorageService(), new Playr.Application.Tests.Badges.NoOpBadgeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<ChatService>.Instance);
            var fixture = new InvitationFixture(connection, dbContext, chatService, new InvitationService(dbContext, chatService, new NoOpInvitationNotifier(), new Playr.Application.Tests.Badges.NoOpBadgeService(), new ProfileService(dbContext, new Playr.Application.Tests.Posts.NoOpFileStorageService(), new Playr.Application.Tests.Profiles.NoOpProfilePresenceNotifier()), Microsoft.Extensions.Logging.Abstractions.NullLogger<InvitationService>.Instance));
            fixture.AddUser(fixture.SenderUserId, "sender", "Sender");
            fixture.AddUser(fixture.RecipientUserId, "recipient", "Recipient");
            await dbContext.SaveChangesAsync();
            return fixture;
        }

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
