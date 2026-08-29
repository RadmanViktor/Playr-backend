namespace Playr.Api.Models.Common;

public sealed record MentionResponse(Guid UserId, string Username, string DisplayName);
