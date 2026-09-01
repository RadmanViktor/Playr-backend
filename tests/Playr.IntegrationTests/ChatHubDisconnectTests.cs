using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Playr.Api.Hubs;
using Playr.Application.Common;
using Playr.Application.Profiles;

namespace Playr.IntegrationTests;

/// <summary>
/// Regression tests for the disconnect -> "mark offline" flow in <see cref="ChatHub"/>.
///
/// The hub's DI scope (and everything resolved from it, e.g. the scoped
/// <see cref="IProfileService"/>/DbContext) is disposed as soon as
/// <see cref="Hub.OnDisconnectedAsync"/> returns. The offline-marking logic runs later, after
/// a grace period, as a fire-and-forget continuation - so it must not rely on anything
/// resolved in the hub's constructor. These tests verify the continuation instead resolves a
/// fresh, scoped <see cref="IProfileService"/> via <see cref="IServiceScopeFactory"/>, and that
/// it actually gets called (this is what a naive "reuse the constructor-injected service"
/// implementation fails to do).
/// </summary>
public sealed class ChatHubDisconnectTests
{
    [Fact]
    public async Task OnDisconnectedAsync_marks_user_offline_using_a_freshly_scoped_profile_service()
    {
        var userId = Guid.NewGuid();

        // Simulates the constructor-injected IProfileService, whose backing scope
        // (and DbContext) is disposed once OnDisconnectedAsync returns. Calling it from the
        // delayed continuation should never happen - if it does, the bug has regressed.
        var disposedProfileService = new ThrowingProfileService();

        // Simulates a fresh service resolved from a *new* scope created after the delay -
        // this is the one that should actually be called.
        var freshProfileService = new RecordingProfileService();

        var services = new ServiceCollection();
        services.AddScoped<IProfileService>(_ => freshProfileService);
        var provider = services.BuildServiceProvider();

        var connectionTracker = new UserConnectionTracker();
        connectionTracker.AddConnection(userId, "connection-1");
        connectionTracker.RemoveConnection(userId, "connection-1"); // -> 0 remaining connections

        var hub = new ChatHub(
            disposedProfileService,
            connectionTracker,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ChatHub>.Instance)
        {
            Context = new FakeHubCallerContext(userId, "connection-1"),
        };

        await hub.OnDisconnectedAsync(exception: null);

        // Give the fire-and-forget continuation time to run past the grace period.
        await Task.Delay(TimeSpan.FromSeconds(9));

        Assert.True(freshProfileService.SetOfflineCalled);
        Assert.False(disposedProfileService.WasCalled);
    }

    private sealed class ThrowingProfileService : IProfileService
    {
        public bool WasCalled { get; private set; }

        public Task SetOfflineAsync(Guid userId, CancellationToken cancellationToken)
        {
            WasCalled = true;
            throw new ObjectDisposedException("PlayrDbContext");
        }

        public Task SetOnlineIfOfflineAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto?> GetByUsernameAsync(string username, Guid? currentUserId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto> UpdateCurrentUserAsync(Guid userId, UpdateProfileCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto> UpdateStatusAsync(Guid userId, UpdateStatusCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto> UpdateAvatarAsync(Guid userId, string baseUrl, FileUploadInput avatar, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto> UpdateCoverImageAsync(Guid userId, string baseUrl, FileUploadInput coverImage, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto> UpdateCoverImagePositionAsync(Guid userId, double positionX, double positionY, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ClearLookingForGameStatusAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ProfileSearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<LookingForGamePlayerDto>> GetLookingForGamePlayersAsync(Guid currentUserId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingProfileService : IProfileService
    {
        public bool SetOfflineCalled { get; private set; }

        public Task SetOfflineAsync(Guid userId, CancellationToken cancellationToken)
        {
            SetOfflineCalled = true;
            return Task.CompletedTask;
        }

        public Task SetOnlineIfOfflineAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto?> GetByUsernameAsync(string username, Guid? currentUserId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto> UpdateCurrentUserAsync(Guid userId, UpdateProfileCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto> UpdateStatusAsync(Guid userId, UpdateStatusCommand command, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto> UpdateAvatarAsync(Guid userId, string baseUrl, FileUploadInput avatar, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto> UpdateCoverImageAsync(Guid userId, string baseUrl, FileUploadInput coverImage, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ProfileDto> UpdateCoverImagePositionAsync(Guid userId, double positionX, double positionY, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ClearLookingForGameStatusAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ProfileSearchResult>> SearchAsync(string query, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<LookingForGamePlayerDto>> GetLookingForGamePlayersAsync(Guid currentUserId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeHubCallerContext : HubCallerContext
    {
        public FakeHubCallerContext(Guid userId, string connectionId)
        {
            ConnectionId = connectionId;
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                authenticationType: "Test"));
        }

        public override string ConnectionId { get; }
        public override string? UserIdentifier => User?.Identity?.Name;
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted { get; } = CancellationToken.None;

        public override void Abort()
        {
        }
    }
}
