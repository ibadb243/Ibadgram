using Application.CQRS.Users.Commands.UpdateShortname;
using Application.CQRS.Users.Commands.UpdateUser;
using Application.CQRS.Users.Queries;
using Application.CQRS.Users.Queries.Get;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
        /// Gets user information by user ID
        /// </summary>
        [HttpGet("{userId:guid}")]
        public async Task<IActionResult> GetUserAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Get user request received for user: {UserId}", userId);

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
                        return Ok(new { IsDeleted = true, Message = "User account has been deleted" });
                    }

                    _logger.LogInformation("User data retrieved successfully for user: {UserId}", userId);
                    return Ok(new GetUserResponse
                    {
                        Firstname = user.Firstname,
                        Lastname = user.Lastname,
                        Shortname = user.Shortname,
                        Bio = user.Bio,
                        Status = user.Status.ToString(),
                        LastSeenAt = user.LastSeenAt
                    });
                }

                _logger.LogWarning("Get user failed for user {UserId}: {Errors}",
                    userId, string.Join(", ", result.Errors.Select(e => e.Message)));
                return NotFound(new { Errors = result.Errors.Select(e => e.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during get user for user: {UserId}", userId);
                return StatusCode(500, new { Message = "An unexpected error occurred while retrieving user information" });
            }
        }

        /// <summary>
        /// Gets current authenticated user's profile
        /// </summary>
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUserAsync(CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                _logger.LogWarning("Get current user failed - user ID not found in token");
                return Unauthorized(new { Message = "User not authenticated" });
            }

            _logger.LogInformation("Get current user request for user: {UserId}", currentUserId.Value);

            return await GetUserAsync(currentUserId.Value, cancellationToken);
        }

        /// <summary>
        /// Updates current user's profile information
        /// </summary>
        [HttpPut("me")]
        public async Task<IActionResult> UpdateUserAsync(
            [FromBody] UpdateUserRequest request,
            CancellationToken cancellationToken)
        {
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                _logger.LogWarning("Update user failed - user ID not found in token");
                return Unauthorized(new { Message = "User not authenticated" });
            }

            _logger.LogInformation("Update user request received for user: {UserId}", currentUserId.Value);

            try
            {
                var command = new UpdateUserCommand
                {
                    UserId = currentUserId.Value,
                    Firstname = request.Firstname,
                    Lastname = request.Lastname,
                    Bio = request.Bio
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("User updated successfully for user: {UserId}", currentUserId.Value);
                    return Ok(new { Message = "User profile updated successfully" });
                }

                _logger.LogWarning("Update user failed for user {UserId}: {Errors}",
                    currentUserId.Value, string.Join(", ", result.Errors.Select(e => e.Message)));
                return BadRequest(new { Errors = result.Errors.Select(e => e.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during update user for user: {UserId}", currentUserId.Value);
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
            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                _logger.LogWarning("Update shortname failed - user ID not found in token");
                return Unauthorized(new { Message = "User not authenticated" });
            }

            _logger.LogInformation("Update shortname request received for user: {UserId}", currentUserId.Value);

            try
            {
                var command = new UpdateShortnameCommand
                {
                    UserId = currentUserId.Value,
                    Shortname = request.Shortname
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Shortname updated successfully for user: {UserId}", currentUserId.Value);
                    return Ok(new { Message = "Shortname updated successfully" });
                }

                _logger.LogWarning("Update shortname failed for user {UserId}: {Errors}",
                    currentUserId.Value, string.Join(", ", result.Errors.Select(e => e.Message)));
                return BadRequest(new { Errors = result.Errors.Select(e => e.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during update shortname for user: {UserId}", currentUserId.Value);
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
    }
}
