using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Chat;
using Playr.Domain.Friendships;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Chat;
using Playr.Infrastructure.Data;

namespace Playr.Application.Tests.Chat;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task GetOrCreateDirectConversationAsync_WhenUsersAreNotFriends_CreatesConversation()
    {
        await using var fixture = await ChatFixture.CreateAsync();

        var conversation = await fixture.Service.GetOrCreateDirectConversationAsync(
            fixture.CurrentUserId,
            fixture.OtherUserId,
            CancellationToken.None);

        conversation.OtherParticipant.UserId.Should().Be(fixture.OtherUserId);
        fixture.DbContext.Conversations.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetOrCreateDirectConversationAsync_WhenUsersAreFriends_CreatesSingleConversation()
    {
        await using var fixture = await ChatFixture.CreateAsync();
        await fixture.AddFriendshipAsync();

        var first = await fixture.Service.GetOrCreateDirectConversationAsync(
            fixture.CurrentUserId,
            fixture.OtherUserId,
            CancellationToken.None);
        var second = await fixture.Service.GetOrCreateDirectConversationAsync(
            fixture.CurrentUserId,
            fixture.OtherUserId,
            CancellationToken.None);

        second.Id.Should().Be(first.Id);
        first.OtherParticipant.UserId.Should().Be(fixture.OtherUserId);
        fixture.DbContext.Conversations.Should().HaveCount(1);
        fixture.DbContext.ConversationParticipants.Should().HaveCount(2);
    }

    [Fact]
    public async Task SendMessageAsync_WhenUserIsParticipant_TrimsAndStoresMessage()
    {
        await using var fixture = await ChatFixture.CreateAsync();
        await fixture.AddFriendshipAsync();
        var conversation = await fixture.Service.GetOrCreateDirectConversationAsync(
            fixture.CurrentUserId,
            fixture.OtherUserId,
            CancellationToken.None);

        var message = await fixture.Service.SendMessageAsync(
            fixture.CurrentUserId,
            conversation.Id,
            new SendChatMessageCommand("  hello there  "),
            CancellationToken.None);

        message.Body.Should().Be("hello there");
        message.SenderUserId.Should().Be(fixture.CurrentUserId);
        var messages = await fixture.Service.GetMessagesAsync(fixture.OtherUserId, conversation.Id, CancellationToken.None);
        messages.Should().ContainSingle().Which.Body.Should().Be("hello there");
    }

    [Fact]
    public async Task SendMessageAsync_WhenMessageIsEmpty_Throws()
    {
        await using var fixture = await ChatFixture.CreateAsync();
        await fixture.AddFriendshipAsync();
        var conversation = await fixture.Service.GetOrCreateDirectConversationAsync(
            fixture.CurrentUserId,
            fixture.OtherUserId,
            CancellationToken.None);

        var act = () => fixture.Service.SendMessageAsync(
            fixture.CurrentUserId,
            conversation.Id,
            new SendChatMessageCommand("   "),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Message is required.");
    }

    private sealed class ChatFixture : IAsyncDisposable
    {
        private ChatFixture(SqliteConnection connection, PlayrDbContext dbContext, ChatService service)
        {
            Connection = connection;
            DbContext = dbContext;
            Service = service;
        }

        public SqliteConnection Connection { get; }
        public PlayrDbContext DbContext { get; }
        public ChatService Service { get; }
        public Guid CurrentUserId { get; } = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public Guid OtherUserId { get; } = Guid.Parse("10000000-0000-0000-0000-000000000002");

        public static async Task<ChatFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PlayrDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new PlayrDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            var fixture = new ChatFixture(connection, dbContext, new ChatService(dbContext, new NoOpChatNotifier()));
            fixture.AddUser(fixture.CurrentUserId, "player", "Player");
            fixture.AddUser(fixture.OtherUserId, "friend", "Friend");
            await dbContext.SaveChangesAsync();
            return fixture;
        }

        public async Task AddFriendshipAsync()
        {
            DbContext.Friendships.Add(new Friendship
            {
                Id = Guid.NewGuid(),
                UserAId = CurrentUserId,
                UserBId = OtherUserId,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await DbContext.SaveChangesAsync();
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
