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

namespace Playr.Application.Tests.Invitations;

public sealed class InvitationServiceTests
{
    [Fact]
    public async Task AcceptAsync_CreatesConversationSeededWithInvitationMessage()
    {
        await using var fixture = await InvitationFixture.CreateAsync();
        var sent = await fixture.Service.SendAsync(
            fixture.SenderUserId,
            new SendInvitationCommand(fixture.RecipientUserId, "Wanna play Apex tonight?"),
            CancellationToken.None);

        await fixture.Service.AcceptAsync(fixture.RecipientUserId, sent.Id, CancellationToken.None);

        fixture.DbContext.Conversations.Should().HaveCount(1);
        var messages = await fixture.DbContext.ChatMessages.AsNoTracking().ToListAsync();
        messages.Should().ContainSingle();
        messages[0].Body.Should().Be("Wanna play Apex tonight?");
        messages[0].SenderUserId.Should().Be(fixture.SenderUserId);
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
        private InvitationFixture(SqliteConnection connection, PlayrDbContext dbContext, InvitationService service)
        {
            Connection = connection;
            DbContext = dbContext;
            Service = service;
        }

        public SqliteConnection Connection { get; }
        public PlayrDbContext DbContext { get; }
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
            var chatService = new ChatService(dbContext, new NoOpChatNotifier(), new NoOpFileStorageService());
            var fixture = new InvitationFixture(connection, dbContext, new InvitationService(dbContext, chatService, new NoOpInvitationNotifier(), new Playr.Application.Tests.Badges.NoOpBadgeService(), Microsoft.Extensions.Logging.Abstractions.NullLogger<InvitationService>.Instance));
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
