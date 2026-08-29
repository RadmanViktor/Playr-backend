using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Playr.Api.Extensions;
using Playr.Application.Profiles;

namespace Playr.Api.Hubs;

[Authorize]
public sealed class ChatHub(IProfileService profileService) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Context.User?.TryGetUserId(out var userId) == true)
        {
            await profileService.SetOnlineIfOfflineAsync(userId, Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.User?.TryGetUserId(out var userId) == true)
        {
            await profileService.SetOfflineAsync(userId, CancellationToken.None);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
