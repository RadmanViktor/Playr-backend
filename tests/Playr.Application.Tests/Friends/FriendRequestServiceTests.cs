using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Application.Friends;
using Playr.Domain.Friendships;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Friends;

namespace Playr.Application.Tests.Friends;

public sealed class FriendRequestServiceTests
{
    [Fact]
    public async Task SendAsync_ToSelf_Throws()
    {
        await using var fixture = await FriendRequestFixture.CreateAsync();

        var act = () => fixture.Service.SendAsync(
            fixture.CurrentUserId,
            new SendFriendRequestCommand(fixture.CurrentUserId),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("You cannot send a friend request to yourself.");
    }

    [Fact]
    public async Task SendAsync_WhenAlreadyFriends_Throws()
    {
        await using var fixture = await FriendRequestFixture.CreateAsync();
        await fixture.AddFriendshipAsync();

        var act = () => fixture.Service.SendAsync(
            fixture.CurrentUserId,
            new SendFriendRequestCommand(fixture.OtherUserId),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("You are already friends with this player.");
    }

    [Fact]
    public async Task SendAsync_WhenPendingRequestExists_Throws()
    {
        await using var fixture = await FriendRequestFixture.CreateAsync();
        await fixture.Service.SendAsync(
            fixture.CurrentUserId,
            new SendFriendRequestCommand(fixture.OtherUserId),
            CancellationToken.None);

        var act = () => fixture.Service.SendAsync(
            fixture.OtherUserId,
            new SendFriendRequestCommand(fixture.CurrentUserId),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("There is already a pending friend request between you and this player.");
    }

    [Fact]
    public async Task AcceptAsync_CreatesFriendship()
    {
        await using var fixture = await FriendRequestFixture.CreateAsync();
        var sent = await fixture.Service.SendAsync(
            fixture.CurrentUserId,
            new SendFriendRequestCommand(fixture.OtherUserId),
            CancellationToken.None);

        var accepted = await fixture.Service.AcceptAsync(fixture.OtherUserId, sent.Id, CancellationToken.None);

        accepted.Status.Should().Be(FriendRequestStatus.Accepted);
        fixture.DbContext.Friendships.Should().HaveCount(1);
    }

    [Fact]
    public async Task AcceptAsync_WhenNotRecipient_Throws()
    {
        await using var fixture = await FriendRequestFixture.CreateAsync();
        var sent = await fixture.Service.SendAsync(
            fixture.CurrentUserId,
            new SendFriendRequestCommand(fixture.OtherUserId),
            CancellationToken.None);

        var act = () => fixture.Service.AcceptAsync(fixture.CurrentUserId, sent.Id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only the recipient can accept this friend request.");
    }

    [Fact]
    public async Task DeclineAsync_SetsStatusAndDoesNotCreateFriendship()
    {
        await using var fixture = await FriendRequestFixture.CreateAsync();
        var sent = await fixture.Service.SendAsync(
            fixture.CurrentUserId,
            new SendFriendRequestCommand(fixture.OtherUserId),
            CancellationToken.None);

        var declined = await fixture.Service.DeclineAsync(fixture.OtherUserId, sent.Id, CancellationToken.None);

        declined.Status.Should().Be(FriendRequestStatus.Declined);
        fixture.DbContext.Friendships.Should().BeEmpty();
    }

    [Fact]
    public async Task CancelAsync_WhenNotSender_Throws()
    {
        await using var fixture = await FriendRequestFixture.CreateAsync();
        var sent = await fixture.Service.SendAsync(
            fixture.CurrentUserId,
            new SendFriendRequestCommand(fixture.OtherUserId),
            CancellationToken.None);

        var act = () => fixture.Service.CancelAsync(fixture.OtherUserId, sent.Id, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Only the sender can cancel this friend request.");
    }

    private sealed class FriendRequestFixture : IAsyncDisposable
    {
        private FriendRequestFixture(SqliteConnection connection, PlayrDbContext dbContext, FriendRequestService service)
        {
            Connection = connection;
            DbContext = dbContext;
            Service = service;
        }

        public SqliteConnection Connection { get; }
        public PlayrDbContext DbContext { get; }
        public FriendRequestService Service { get; }
        public Guid CurrentUserId { get; } = Guid.Parse("30000000-0000-0000-0000-000000000001");
        public Guid OtherUserId { get; } = Guid.Parse("30000000-0000-0000-0000-000000000002");

        public static async Task<FriendRequestFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PlayrDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new PlayrDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            var fixture = new FriendRequestFixture(connection, dbContext, new FriendRequestService(dbContext));
            fixture.AddUser(fixture.CurrentUserId, "player", "Player");
            fixture.AddUser(fixture.OtherUserId, "friend", "Friend");
            await dbContext.SaveChangesAsync();
            return fixture;
        }

        public async Task AddFriendshipAsync()
        {
            var (userAId, userBId) = CurrentUserId.CompareTo(OtherUserId) < 0
                ? (CurrentUserId, OtherUserId)
                : (OtherUserId, CurrentUserId);
            DbContext.Friendships.Add(new Friendship
            {
                Id = Guid.NewGuid(),
                UserAId = userAId,
                UserBId = userBId,
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
