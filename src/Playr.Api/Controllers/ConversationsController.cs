using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Playr.Api.Extensions;
using Playr.Api.Models.Chat;
using Playr.Application.Chat;

namespace Playr.Api.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize]
public sealed class ConversationsController(IChatService chatService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConversationResponse>>> GetConversations(CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        var conversations = await chatService.GetConversationsAsync(userId, cancellationToken);
        return Ok(conversations.Select(ToResponse).ToList());
    }

    [HttpPost("with/{otherUserId:guid}")]
    public async Task<ActionResult<ConversationResponse>> GetOrCreateWith(Guid otherUserId, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var conversation = await chatService.GetOrCreateDirectConversationAsync(userId, otherUserId, cancellationToken);
            return Ok(ToResponse(conversation));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageResponse>>> GetMessages(Guid id, CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var messages = await chatService.GetMessagesAsync(userId, id, cancellationToken);
            return Ok(messages.Select(ToResponse).ToList());
        }
        catch (InvalidOperationException ex) when (ex.Message == "You are not part of this conversation.")
        {
            return StatusCode(403, new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<ChatMessageResponse>> SendMessage(
        Guid id,
        SendChatMessageRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetUserId(out var userId))
        {
            return Unauthorized(new { error = "User id claim is missing or invalid." });
        }

        try
        {
            var message = await chatService.SendMessageAsync(userId, id, new SendChatMessageCommand(request.Body), cancellationToken);
            return Ok(ToResponse(message));
        }
        catch (InvalidOperationException ex) when (ex.Message is "Conversation was not found.")
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message == "You are not part of this conversation.")
        {
            return StatusCode(403, new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private static ConversationResponse ToResponse(ConversationDto conversation) => new(
        conversation.Id,
        new ChatParticipantResponse(
            conversation.OtherParticipant.UserId,
            conversation.OtherParticipant.Username,
            conversation.OtherParticipant.DisplayName,
            conversation.OtherParticipant.AvatarUrl),
        conversation.LastMessage,
        conversation.LastMessageAt,
        conversation.CreatedAt,
        conversation.UpdatedAt);

    private static ChatMessageResponse ToResponse(ChatMessageDto message) => new(
        message.Id,
        message.ConversationId,
        message.SenderUserId,
        message.SenderUsername,
        message.SenderDisplayName,
        message.SenderAvatarUrl,
        message.Body,
        message.CreatedAt,
        message.ReadAt);
}
