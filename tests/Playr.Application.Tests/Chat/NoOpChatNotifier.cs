using Playr.Application.Chat;

namespace Playr.Application.Tests.Chat;

public sealed class NoOpChatNotifier : IChatNotifier
{
    public Task NotifyNewMessageAsync(
        IReadOnlyCollection<Guid> recipientUserIds,
        ChatMessageDto message,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
