namespace Playr.Application.Posts;

public sealed record PostMediaInput(Stream Content, string FileName, string ContentType, long Length);
