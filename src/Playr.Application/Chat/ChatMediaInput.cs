namespace Playr.Application.Chat;

public sealed record ChatMediaInput(Stream Content, string FileName, string ContentType, long Length);
