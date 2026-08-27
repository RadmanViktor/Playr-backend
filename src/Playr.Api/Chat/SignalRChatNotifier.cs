using Microsoft.AspNetCore.SignalR;
using Playr.Api.Hubs;
using Playr.Application.Chat;

namespace Playr.Api.Chat;

public sealed class SignalRChatNotifier(IHubContext<ChatHub> hubContext) : IChatNotifier
{
    public async Task NotifyNewMessageAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        ChatMessageDto message,
        CancellationToken cancellationToken)
    {
        var userIds = recipientUserIds.Select(id => id.ToString()).ToList();
        if (userIds.Count == 0)
        {
            return;
        }

        await hubContext.Clients.Users(userIds).SendAsync("ReceiveMessage", message, cancellationToken);
    }
}
