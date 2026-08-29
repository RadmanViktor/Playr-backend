using Microsoft.AspNetCore.Http;

namespace Playr.Api.Models.Chat;

public sealed class SendChatMessageRequest
{
    public string? Body { get; set; }

    public IFormFile? Media { get; set; }
}
