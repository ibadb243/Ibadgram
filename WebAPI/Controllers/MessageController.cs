using Application.CQRS.Messages.Commands.SendMessage;
using Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Extensions;
using WebAPI.Models.DTOs.Message;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/messages/")]
    [Authorize(Policy = "Standart")]
    public class MessageController : BaseController
    {
        private readonly ILogger<MessageController> _logger;

        public MessageController(
            IMediator mediator,
            ILogger<MessageController> logger)
                : base(mediator)
        {
            _logger = logger;
        }

        /// <summary>
        /// Sends a message to a chat
        /// </summary>
        /// <param name="request">Message details</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created message ID</returns>
        [HttpPost("send")]
        public async Task<IActionResult> SendMessageAsync(
            [FromBody] SendMessageRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();

            _logger.LogInformation("Send message request received from user: {UserId} to chat: {ChatId}",
                userId, request.ChatId);

            try
            {
                var command = new SendMessageCommand
                {
                    UserId = userId,
                    ChatId = request.ChatId,
                    Message = request.Message?.Trim() ?? string.Empty
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    var response = result.Value;
                    _logger.LogInformation("Message sent successfully with ID: {MessageId} by user: {UserId} to chat: {ChatId}",
                        response.Id, userId, request.ChatId);

                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            messageId = response.Id
                        },
                        message = "Message sent successfully"
                    });
                }

                _logger.LogWarning("Send message failed for user {UserId} to chat {ChatId}: {Errors}",
                    userId, request.ChatId, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during message sending for user: {UserId} to chat: {ChatId}",
                    userId, request.ChatId);

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.DATABASE_ERROR,
                        message = "An unexpected error occurred while sending the message"
                    }
                });
            }
        }
    }
}
