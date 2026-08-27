using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Playr.Api.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
}
