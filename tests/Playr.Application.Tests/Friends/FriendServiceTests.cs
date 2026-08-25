using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Playr.Domain.Friendships;
using Playr.Domain.Identity;
using Playr.Domain.Profiles;
using Playr.Infrastructure.Data;
using Playr.Infrastructure.Friends;

namespace Playr.Application.Tests.Friends;

public sealed class FriendServiceTests
{
    [Fact]
    public async Task GetFriendsAsync_ReturnsFriendsForEitherSideOfFriendship()
    {
        await using var fixture = await FriendFixture.CreateAsync();

        var friends = await fixture.Service.GetFriendsAsync(fixture.CurrentUserId, CancellationToken.None);

        friends.Should().ContainSingle();
        friends[0].UserId.Should().Be(fixture.OtherUserId);
        friends[0].Username.Should().Be("friend");
        friends[0].DisplayName.Should().Be("Friend");
    }

    private sealed class FriendFixture : IAsyncDisposable
    {
        private FriendFixture(SqliteConnection connection, PlayrDbContext dbContext, FriendService service)
        {
            Connection = connection;
            DbContext = dbContext;
            Service = service;
        }

        public SqliteConnection Connection { get; }
        public PlayrDbContext DbContext { get; }
        public FriendService Service { get; }
        public Guid CurrentUserId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000001");
        public Guid OtherUserId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000002");

        public static async Task<FriendFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<PlayrDbContext>()
                .UseSqlite(connection)
                .Options;
            var dbContext = new PlayrDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();
            var fixture = new FriendFixture(connection, dbContext, new FriendService(dbContext));
            fixture.AddUser(fixture.CurrentUserId, "player", "Player");
            fixture.AddUser(fixture.OtherUserId, "friend", "Friend");
            dbContext.Friendships.Add(new Friendship
            {
                Id = Guid.NewGuid(),
                UserAId = fixture.OtherUserId,
                UserBId = fixture.CurrentUserId,
                CreatedAt = DateTimeOffset.UtcNow
            });
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
