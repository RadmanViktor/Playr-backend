namespace Playr.Application.Chat;

/// <summary>
/// Pushes chat events to connected clients in real time (e.g. via SignalR).
/// Implemented in the API layer, where the transport (hub) lives.
/// </summary>
public interface IChatNotifier
{
    Task NotifyNewMessageAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        ChatMessageDto message,
        CancellationToken cancellationToken);
}
