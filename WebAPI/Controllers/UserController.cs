using Application.CQRS.Memberships.Queries.UserMembership;
using Application.CQRS.Users.Commands.UpdateShortname;
using Application.CQRS.Users.Commands.UpdateUser;
using Application.CQRS.Users.Queries;
using Application.CQRS.Users.Queries.Get;
using Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using WebAPI.Extensions;
using WebAPI.Models.DTOs.User;

namespace WebAPI.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Policy = "Standart")]
    public class UserController : BaseController
    {
        private readonly ILogger<UserController> _logger;

        public UserController(IMediator mediator, ILogger<UserController> logger)
            : base(mediator)
        {
            _logger = logger;
        }



        /// <summary>
        /// Return information about user
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Info about self user</returns>
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUserAsync(CancellationToken cancellationToken)
        {
            var currentUserId = User.GetUserId();

            return await GetUserAsync(currentUserId, cancellationToken);
        }




        /// <summary>
        /// Return information about user with ID
        /// </summary>
        /// <param name="userId">User Id</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Info about User</returns>
        [HttpGet("{userId:guid}")]
        public async Task<IActionResult> GetUserAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Get user request recieved for user: {UserId}", userId);

            try
            {
                var query = new GetUserQuery
                {
                    UserId = userId
                };

                var result = await Mediator.Send(query, cancellationToken);

                if (result.IsSuccess)
                {
                    var user = result.Value;

                    // Если пользователь удалён, возвращаем минимальную информацию
                    if (user.IsDeleted.HasValue && user.IsDeleted.Value)
                    {
                        _logger.LogInformation("Returning deleted user info for user: {UserId}", userId);
                        return Ok(new 
                        { 
                            success = true,
                            data = new
                            {
                                IsDeleted = true,
                            },
                            message = "User account has been deleted"
                        });
                    }

                    _logger.LogInformation("User data retrieved successfully for user: {UserId}", userId);
                    return Ok(new
                    {
                        success = true,
                        data = new GetUserResponse
                        {
                            Firstname = user.Firstname,
                            Lastname = user.Lastname,
                            Shortname = user.Shortname,
                            Bio = user.Bio,
                            Status = user.Status.ToString(),
                            LastSeenAt = user.LastSeenAt
                        }
                    });
                }

                _logger.LogWarning("User information recievation failed with Id {UserId}: {Errors}",
                    userId, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during get user: {UserId}", userId);

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.DATABASE_ERROR,
                        message = "An unexpected error occurred during get user"
                    }
                });
            }
        }



        /// <summary>
        /// Return user memberships
        /// </summary>
        /// <param name="cancellationToken">Cancelation token</param>
        /// <returns></returns>
        [HttpGet("chats")]
        public async Task<IActionResult> GetUserChatsAsync(
            CancellationToken cancellationToken)
        {
            var currentUserId = User.GetUserId();

            _logger.LogInformation("Retrieval user memberships for user: {UserId}",
                currentUserId);

            try
            {
                var command = new GetUserMembershipQuery
                {
                    UserId = currentUserId,
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    var response = result.Value;
                    _logger.LogInformation("Retrieval user memberships successfully with chat count: {ChatCount}", response.Chats.Count());

                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            chats = response.Chats,
                            count = response.Chats.Count()
                        },
                        message = "User memberships retrieved successfully"
                    });
                }

                _logger.LogWarning("Retrieval user memberships failed for user {UserId}: {Errors}",
                   currentUserId, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during retrieval user memberships for user: {UserId}", currentUserId);

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.DATABASE_ERROR,
                        message = "An unexpected error occurred while retrieval user memberships"
                    }
                });
            }
        }



        /// <summary>
        /// Updates current user's profile information
        /// </summary>
        [HttpPut("me")]
        public async Task<IActionResult> UpdateUserAsync(
            [FromBody] UpdateUserRequest request,
            CancellationToken cancellationToken)
        {
            var currentUserId = User.GetUserId();

            _logger.LogInformation("Update user request received for user: {UserId}", currentUserId);

            try
            {
                var command = new UpdateUserCommand
                {
                    UserId = currentUserId,
                    Firstname = request.Firstname,
                    Lastname = request.Lastname,
                    Bio = request.Bio
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("User updated successfully for user: {UserId}", currentUserId);
                    return Ok(new { Message = "User profile updated successfully" });
                }

                _logger.LogWarning("Update user failed for user {UserId}: {Errors}",
                    currentUserId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return BadRequest(new { Errors = result.Errors.Select(e => e.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during update user for user: {UserId}", currentUserId);
                return StatusCode(500, new { Message = "An unexpected error occurred while updating user profile" });
            }
        }

        /// <summary>
        /// Updates current user's shortname (username)
        /// </summary>
        [HttpPut("me/shortname")]
        public async Task<IActionResult> UpdateShortnameAsync(
            [FromBody] UserUpdateShortnameRequest request,
            CancellationToken cancellationToken)
        {
            var currentUserId = User.GetUserId();

            _logger.LogInformation("Update shortname request received for user: {UserId}", currentUserId);

            try
            {
                var command = new UpdateShortnameCommand
                {
                    UserId = currentUserId,
                    Shortname = request.Shortname
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Shortname updated successfully for user: {UserId}", currentUserId);
                    return Ok(new { Message = "Shortname updated successfully" });
                }

                _logger.LogWarning("Update shortname failed for user {UserId}: {Errors}",
                    currentUserId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return BadRequest(new { Errors = result.Errors.Select(e => e.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during update shortname for user: {UserId}", currentUserId);
                return StatusCode(500, new { Message = "An unexpected error occurred while updating shortname" });
            }
        }

        /// <summary>
        /// Searches users by shortname (for public access)
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous] // Поиск пользователей доступен без авторизации
        public async Task<IActionResult> SearchUsersByShortname(
            [FromQuery] string shortname,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(shortname))
            {
                _logger.LogWarning("Search users failed - empty shortname provided");
                return BadRequest(new { Message = "Shortname parameter is required" });
            }

            _logger.LogInformation("Search users request received for shortname: {Shortname}", shortname);

            try
            {
                // Здесь можно было бы реализовать поисковой запрос
                // Для примера возвращаем заглушку
                _logger.LogInformation("Search functionality not implemented yet");
                return Ok(new { Message = "Search functionality will be implemented in future updates" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during user search for shortname: {Shortname}", shortname);
                return StatusCode(500, new { Message = "An unexpected error occurred during search" });
            }
        }
    }
}
