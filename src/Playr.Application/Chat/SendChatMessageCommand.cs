namespace Playr.Application.Chat;

public sealed record SendChatMessageCommand(string? Body, ChatMediaInput? Media);
