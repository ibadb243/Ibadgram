using Application.CQRS.Chats.Commands.CreateChat;
using Application.CQRS.Chats.Commands.CreateGroup;
using Application.CQRS.Chats.Commands.DeleteGroup;
//using Application.CQRS.Chats.Commands.MakePrivateGroup;
//using Application.CQRS.Chats.Commands.MakePublicGroup;
//using Application.CQRS.Chats.Commands.UpdateGroup;
//using Application.CQRS.Chats.Commands.UpdateShortname;
using Application.CQRS.Chats.Queries;
using Application.CQRS.Chats.Queries.GetGroupMembers;
using Application.CQRS.Memberships.Queries.UserMembership;
using Application.CQRS.Messages.Queries.GetMessages;
using Domain.Common;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using WebAPI.Extensions;
using WebAPI.Models.DTOs.Chat;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/chats/")]
    [Authorize(Policy = "Standart")]
    public class ChatController : BaseController
    {
        private readonly ILogger<ChatController> _logger;

        public ChatController(IMediator mediator, ILogger<ChatController> logger)
            : base(mediator)
        {
            _logger = logger;
        }


        /// <summary>
        /// Get chat messages
        /// </summary>
        /// <param name="request">Chat messages getting request</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Short chat info, pagination info and list messages</returns>
        [HttpGet("{chatId:guid}/messages")]
        public async Task<IActionResult> GetChatMessages(
            [FromRoute] Guid chatId,
            [FromQuery] GetChatMessagesRequest request,
            CancellationToken cancellationToken)
        {
            var currentUserId = User.GetUserId();

            _logger.LogInformation("Get chat messages request received by user: {UserId}",
                currentUserId);

            try
            {
                var command = new GetMessageListQuery
                {
                    UserId = currentUserId,
                    ChatId = chatId,
                    Limit = request.Limit,
                    Offset = request.Offset,
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    var response = result.Value;

                    _logger.LogInformation("Chat messages gotten successfully");

                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            chat = new
                            {
                                id = response.ChatInfo.ChatId,
                                name = response.ChatInfo.ChatName,
                            },
                            messages = response.Messages.Select(msg => new
                            {
                                id = msg.MessageId,
                                author_id = msg.UserId,
                                author = msg.Fullname,
                                author_nickname = msg.Nickname,
                                text = msg.Text,
                                timestamp = msg.Timestamp,
                                edited = msg.IsEdited,
                            }),
                            total = response.Pagination.TotalCount,
                            offset = response.Pagination.Offset,
                            limit = response.Pagination.Limit,
                            hasNextPage = response.Pagination.HasNextPage,
                        }
                    });
                }

                _logger.LogWarning("Get chat messages failed for user {UserId}: {Errors}",
                    currentUserId, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during get chat messages by user: {UserId}", currentUserId);

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.DATABASE_ERROR,
                        message = "An unexpected error occurred while get chat messages"
                    }
                });
            }
        }


        /// <summary>
        /// Creates a private one-to-one chat
        /// </summary>
        /// <param name="request">Private chat creation details</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created chat ID and confirmation message</returns>
        [HttpPost("chat")]
        public async Task<IActionResult> CreatePrivateChatAsync(
            [FromBody] CreatePrivateChatRequest request,
            CancellationToken cancellationToken)
        {
            var currentUserId = User.GetUserId();

            _logger.LogInformation("Create private chat request received by user: {UserId} for user: {SecondUserId}",
                currentUserId, request.UserId);

            try
            {
                var command = new CreateChatCommand
                {
                    FirstUserId = currentUserId,
                    SecondUserId = request.UserId,
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Private chat created successfully with ID: {ChatId}", result.Value);

                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            chatId = result.Value
                        },
                        message = "Private chat created successfully"
                    });
                }

                _logger.LogWarning("Create private chat failed for users {FirstUserId}-{SecondUserId}: {Errors}",
                    currentUserId, request.UserId, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during create private chat by user: {UserId}", currentUserId);

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.DATABASE_ERROR,
                        message = "An unexpected error occurred while creating private chat"
                    }
                });
            }
        }




        /// <summary>
        /// Creates a new group chat
        /// </summary>
        /// <param name="request">Group creation details</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created group information and confirmation message</returns>
        [HttpPost("group")]
        public async Task<IActionResult> CreateGroupAsync(
            [FromBody] CreateGroupRequest request,
            CancellationToken cancellationToken)
        {
            var currentUserId = User.GetUserId();

            _logger.LogInformation("Create group request received by user: {UserId}, Name: {GroupName}",
                currentUserId, request.Name);

            try
            {
                var command = new CreateGroupCommand
                {
                    UserId = currentUserId,
                    Name = request.Name?.Trim() ?? string.Empty,
                    Description = string.IsNullOrWhiteSpace(request.Description) ? string.Empty : request.Description.Trim(),
                    IsPrivate = request.IsPrivate,
                    Shortname = string.IsNullOrWhiteSpace(request.Shortname) ? null : request.Shortname.Trim(),
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    var response = result.Value;
                    _logger.LogInformation("Group created successfully with ID: {GroupId}", response.GroupId);

                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            group = new
                            {
                                id = response.GroupId,
                                name = response.Name,
                                description = response.Description,
                                is_public = !response.IsPrivate,
                                shortname = response.Shortname,
                                created = response.CreatedAt,
                            },
                        },
                        message = "Group created successfully"
                    });
                }

                _logger.LogWarning("Create group failed for user {UserId}: {Errors}",
                    currentUserId, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during create group by user: {UserId}", currentUserId);

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.DATABASE_ERROR,
                        message = "An unexpected error occurred while creating group"
                    }
                });
            }
        }


    }
}
