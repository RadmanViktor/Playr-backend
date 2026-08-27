using Microsoft.AspNetCore.SignalR;

namespace Playr.Api.Hubs;

/// <summary>
/// Maps SignalR's "user" concept to our JWT-based user id (the "sub" / NameIdentifier claim),
/// so the server can push messages to a specific user via Clients.User(userId).
/// </summary>
public sealed class ChatUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst("sub")?.Value;
}
