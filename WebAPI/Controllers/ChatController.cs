using Application.CQRS.Chats.Commands.CreateChat;
using Application.CQRS.Chats.Commands.CreateGroup;
using Application.CQRS.Chats.Commands.DeleteGroup;
using Application.CQRS.Chats.Commands.MakePrivateGroup;
using Application.CQRS.Chats.Commands.MakePublicGroup;
using Application.CQRS.Chats.Commands.UpdateGroup;
using Application.CQRS.Chats.Commands.UpdateShortname;
using Application.CQRS.Chats.Queries;
using Application.CQRS.Chats.Queries.GetGroupMembers;
using Domain.Common;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Authorization;
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
        /// Creates a private one-to-one chat
        /// </summary>
        /// <param name="request">Private chat creation details</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created chat ID and confirmation message</returns>
        [HttpPost("private")]
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
        [HttpPost("groups")]
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
                            groupId = response.GroupId,
                            name = response.Name,
                            description = response.Description,
                            isPrivate = response.IsPrivate,
                            shortname = response.Shortname,
                            createdAt = response.CreatedAt
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

        #region Need Refactoring

        /// <summary>
        /// Gets chat information by chat ID
        /// </summary>
        [HttpGet("{chatId:guid}")]
        public async Task<IActionResult> GetChatAsync(
            Guid chatId,
            CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                _logger.LogWarning("Get chat failed - user ID not found in token");
                return Unauthorized(new { Message = "User not authenticated" });
            }

            _logger.LogInformation("Get chat request received for chat: {ChatId} by user: {UserId}",
                chatId, currentUserId.Value);

            try
            {
                var query = new GetChatQuery
                {
                    UserId = currentUserId.Value,
                    ChatId = chatId
                };

                var result = await Mediator.Send(query, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Chat data retrieved successfully for chat: {ChatId}", chatId);
                    return Ok(result.Value);
                }

                _logger.LogWarning("Get chat failed for chat {ChatId}: {Errors}",
                    chatId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return BadRequest(new { Errors = result.Errors.Select(e => e.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during get chat for chat: {ChatId}", chatId);
                return StatusCode(500, new { Message = "An unexpected error occurred while retrieving chat information" });
            }
        }

        /// <summary>
        /// Gets group members with pagination and filtering
        /// </summary>
        [HttpGet("{chatId:guid}/members")]
        public async Task<IActionResult> GetGroupMembersAsync(
            Guid chatId,
            [FromQuery] int offset = 0,
            [FromQuery] int limit = 50,
            [FromQuery] string? searchTerm = null,
            [FromQuery] int? roleFilter = null,
            [FromQuery] bool includeDeleted = false,
            CancellationToken cancellationToken = default)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                _logger.LogWarning("Get group members failed - user ID not found in token");
                return Unauthorized(new { Message = "User not authenticated" });
            }

            _logger.LogInformation("Get group members request received for chat: {ChatId} by user: {UserId}",
                chatId, currentUserId.Value);

            try
            {
                var query = new GetGroupMembersQuery
                {
                    UserId = currentUserId.Value,
                    ChatId = chatId,
                    Offset = offset,
                    Limit = limit,
                    SearchTerm = searchTerm,
                    RoleFilter = roleFilter.HasValue ? (Domain.Enums.ChatRole?)roleFilter.Value : null,
                    IncludeDeleted = includeDeleted
                };

                var result = await Mediator.Send(query, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Group members retrieved successfully for chat: {ChatId}, Count: {Count}",
                        chatId, result.Value.Members.Count);
                    return Ok(result.Value);
                }

                _logger.LogWarning("Get group members failed for chat {ChatId}: {Errors}",
                    chatId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return BadRequest(new { Errors = result.Errors.Select(e => e.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during get group members for chat: {ChatId}", chatId);
                return StatusCode(500, new { Message = "An unexpected error occurred while retrieving group members" });
            }
        }

        ///// <summary>
        ///// Creates a private one-to-one chat
        ///// </summary>
        //[HttpPost("private")]
        //public async Task<IActionResult> CreatePrivateChatAsync(
        //    [FromBody] CreatePrivateChatRequest request,
        //    CancellationToken cancellationToken)
        //{
        //    var currentUserId = GetCurrentUserId();
        //    if (!currentUserId.HasValue)
        //    {
        //        _logger.LogWarning("Create private chat failed - user ID not found in token");
        //        return Unauthorized(new { Message = "User not authenticated" });
        //    }

        //    _logger.LogInformation("Create private chat request received by user: {UserId} for user: {SecondUserId}",
        //        currentUserId.Value, request.UserId);

        //    try
        //    {
        //        var command = new CreateChatCommand
        //        {
        //            FirstUserId = currentUserId.Value,
        //            SecondUserId = request.UserId,
        //        };

        //        var result = await Mediator.Send(command, cancellationToken);

        //        if (result.IsSuccess)
        //        {
        //            _logger.LogInformation("Private chat created successfully with ID: {ChatId}", result.Value);
        //            return CreatedAtAction(nameof(GetChatAsync),
        //                new { chatId = result.Value },
        //                new { ChatId = result.Value, Message = "Private chat created successfully" });
        //        }

        //        _logger.LogWarning("Create private chat failed for users {FirstUserId}-{SecondUserId}: {Errors}",
        //            currentUserId.Value, request.UserId, string.Join(", ", result.Errors.Select(e => e.Message)));
        //        return BadRequest(new { Errors = result.Errors.Select(e => e.Message) });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Unexpected error during create private chat by user: {UserId}", currentUserId.Value);
        //        return StatusCode(500, new { Message = "An unexpected error occurred while creating private chat" });
        //    }
        //}

        ///// <summary>
        ///// Creates a new group chat
        ///// </summary>
        //[HttpPost("groups")]
        //public async Task<IActionResult> CreateGroupAsync(
        //    [FromBody] CreateGroupRequest request,
        //    CancellationToken cancellationToken)
        //{
        //    var currentUserId = GetCurrentUserId();
        //    if (!currentUserId.HasValue)
        //    {
        //        _logger.LogWarning("Create group failed - user ID not found in token");
        //        return Unauthorized(new { Message = "User not authenticated" });
        //    }

        //    _logger.LogInformation("Create group request received by user: {UserId}, Name: {GroupName}",
        //        currentUserId.Value, request.Name);

        //    try
        //    {
        //        var command = new CreateGroupCommand
        //        {
        //            UserId = currentUserId.Value,
        //            Name = request.Name,
        //            Description = request.Description ?? string.Empty,
        //            IsPrivate = request.IsPrivate,
        //            Shortname = request.Shortname,
        //        };

        //        var result = await Mediator.Send(command, cancellationToken);

        //        if (result.IsSuccess)
        //        {
        //            _logger.LogInformation("Group created successfully with ID: {GroupId}", result.Value);
        //            return CreatedAtAction(nameof(GetChatAsync),
        //                new { chatId = result.Value },
        //                new { GroupId = result.Value, Message = "Group created successfully" });
        //        }

        //        _logger.LogWarning("Create group failed for user {UserId}: {Errors}",
        //            currentUserId.Value, string.Join(", ", result.Errors.Select(e => e.Message)));
        //        return BadRequest(new { Errors = result.Errors.Select(e => e.Message) });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Unexpected error during create group by user: {UserId}", currentUserId.Value);
        //        return StatusCode(500, new { Message = "An unexpected error occurred while creating group" });
        //    }
        //}

        /// <summary>
        /// Updates group information
        /// </summary>
        [HttpPut("groups/{groupId:guid}")]
        public async Task<IActionResult> UpdateGroupAsync(
            Guid groupId,
            [FromBody] UpdateGroupRequest request,
            CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                _logger.LogWarning("Update group failed - user ID not found in token");
                return Unauthorized(new { Message = "User not authenticated" });
            }

            _logger.LogInformation("Update group request received for group: {GroupId} by user: {UserId}",
                groupId, currentUserId.Value);

            try
            {
                var command = new UpdateGroupCommand
                {
                    UserId = currentUserId.Value,
                    GroupId = groupId,
                    Name = request.Name,
                    Description = request.Description
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Group updated successfully for group: {GroupId}", groupId);
                    return Ok(new { Message = "Group updated successfully" });
                }

                _logger.LogWarning("Update group failed for group {GroupId}: {Errors}",
                    groupId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return BadRequest(new { Errors = result.Errors.Select(e => e.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during update group for group: {GroupId}", groupId);
                return StatusCode(500, new { Message = "An unexpected error occurred while updating group" });
            }
        }

        /// <summary>
        /// Updates group shortname
        /// </summary>
        [HttpPut("groups/{groupId:guid}/shortname")]
        public async Task<IActionResult> UpdateGroupShortnameAsync(
            Guid groupId,
            [FromBody] ChatUpdateShortnameRequest request,
            CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                _logger.LogWarning("Update group shortname failed - user ID not found in token");
                return Unauthorized(new { Message = "User not authenticated" });
            }

            _logger.LogInformation("Update group shortname request received for group: {GroupId} by user: {UserId}",
                groupId, currentUserId.Value);

            try
            {
                var command = new UpdateShortnameCommand
                {
                    UserId = currentUserId.Value,
                    GroupId = groupId,
                    Shortname = request.Shortname
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Group shortname updated successfully for group: {GroupId}", groupId);
                    return Ok(new { Message = "Group shortname updated successfully" });
                }

                _logger.LogWarning("Update group shortname failed for group {GroupId}: {Errors}",
                    groupId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return BadRequest(new { Errors = result.Errors.Select(e => e.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during update group shortname for group: {GroupId}", groupId);
                return StatusCode(500, new { Message = "An unexpected error occurred while updating group shortname" });
            }
        }

        /// <summary>
        /// Makes a public group private
        /// </summary>
        [HttpPut("groups/{groupId:guid}/make-private")]
        public async Task<IActionResult> MakeGroupPrivateAsync(
            Guid groupId,
            CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                _logger.LogWarning("Make group private failed - user ID not found in token");
                return Unauthorized(new { Message = "User not authenticated" });
            }

            _logger.LogInformation("Make group private request received for group: {GroupId} by user: {UserId}",
                groupId, currentUserId.Value);

            try
            {
                var command = new MakePrivateGroupCommand
                {
                    UserId = currentUserId.Value,
                    GroupId = groupId
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Group made private successfully for group: {GroupId}", groupId);
                    return Ok(new { Message = "Group made private successfully" });
                }

                _logger.LogWarning("Make group private failed for group {GroupId}: {Errors}",
                    groupId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return BadRequest(new { Errors = result.Errors.Select(e => e.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during make group private for group: {GroupId}", groupId);
                return StatusCode(500, new { Message = "An unexpected error occurred while making group private" });
            }
        }

        /// <summary>
        /// Makes a private group public
        /// </summary>
        [HttpPut("groups/{groupId:guid}/make-public")]
        public async Task<IActionResult> MakeGroupPublicAsync(
            Guid groupId,
            [FromBody] MakePublicGroupRequest request,
            CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                _logger.LogWarning("Make group public failed - user ID not found in token");
                return Unauthorized(new { Message = "User not authenticated" });
            }

            _logger.LogInformation("Make group public request received for group: {GroupId} by user: {UserId}",
                groupId, currentUserId.Value);

            try
            {
                var command = new MakePublicGroupCommand
                {
                    UserId = currentUserId.Value,
                    GroupId = groupId,
                    Shortname = request.Shortname
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Group made public successfully for group: {GroupId}", groupId);
                    return Ok(new { Message = "Group made public successfully" });
                }

                _logger.LogWarning("Make group public failed for group {GroupId}: {Errors}",
                    groupId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return BadRequest(new { Errors = result.Errors.Select(e => e.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during make group public for group: {GroupId}", groupId);
                return StatusCode(500, new { Message = "An unexpected error occurred while making group public" });
            }
        }

        /// <summary>
        /// Deletes a group
        /// </summary>
        [HttpDelete("groups/{groupId:guid}")]
        public async Task<IActionResult> DeleteGroupAsync(
            Guid groupId,
            CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                _logger.LogWarning("Delete group failed - user ID not found in token");
                return Unauthorized(new { Message = "User not authenticated" });
            }

            _logger.LogInformation("Delete group request received for group: {GroupId} by user: {UserId}",
                groupId, currentUserId.Value);

            try
            {
                var command = new DeleteGroupCommand
                {
                    UserId = currentUserId.Value,
                    GroupId = groupId,
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Group deleted successfully for group: {GroupId}", groupId);
                    return Ok(new { Message = "Group deleted successfully" });
                }

                _logger.LogWarning("Delete group failed for group {GroupId}: {Errors}",
                    groupId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return BadRequest(new { Errors = result.Errors.Select(e => e.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during delete group for group: {GroupId}", groupId);
                return StatusCode(500, new { Message = "An unexpected error occurred while deleting group" });
            }
        }

        /// <summary>
        /// Gets current user ID from JWT token claims
        /// </summary>
        private Guid? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                // Попробуем альтернативные claim типы
                userIdClaim = User.FindFirst("sub")?.Value ?? User.FindFirst("userId")?.Value;
            }

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                return userId;
            }

            _logger.LogWarning("Failed to extract user ID from token claims");
            return null;
        }

        #endregion
    }
}
