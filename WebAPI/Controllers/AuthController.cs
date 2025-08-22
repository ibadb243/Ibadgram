using Application.CQRS.Users.Commands.CompleteAccount;
using Application.CQRS.Users.Commands.ConfirmEmail;
using Application.CQRS.Users.Commands.CreateAccount;
using Application.CQRS.Users.Commands.Login;
using Application.CQRS.Users.Commands.Logout;
using Application.CQRS.Users.Commands.Refresh;
using Application.CQRS.Users.Commands.UpdateConfirmEmailToken;
using Domain.Common;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WebAPI.Extensions;
using WebAPI.Models.DTOs.Auth;

namespace WebAPI.Controllers
{

    [ApiController]
    [Route("api/auth/")]
    public class AuthController : BaseController
    {
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IMediator mediator,
            ILogger<AuthController> logger)
                : base(mediator)
        {
            _logger = logger;
        }




        /// <summary>
        /// Creates a new user account and sends email confirmation
        /// </summary>
        /// <param name="request">Account creation details</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Created user ID and confirmation message</returns>
        [HttpPost("register")]
        public async Task<IActionResult> CreateAccountAsync(
            [FromBody] CreateAccountRequest request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Account creation request received for email: {Email}", request.Email);

            try
            {
                var command = new CreateAccountCommand
                {
                    Firstname = request.Firstname?.Trim() ?? string.Empty,
                    Lastname = request.Lastname?.Trim(),
                    Email = request.Email?.Trim() ?? string.Empty,
                    Password = request.Password ?? string.Empty
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Account created successfully with ID: {UserId}", result.Value);

                    // Set secure HTTP-only cookies
                    SetSecureTemporaryTokenCookie(result.Value.TemporaryAccessToken);

                    return Ok(new
                    {
                        success = true,
                        message = "Account created successfully. Please check your email for confirmation code."
                    });
                }

                _logger.LogWarning("Account creation failed for email {Email}: {Errors}",
                    request.Email, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during account creation for email: {Email}", request.Email);

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.DATABASE_ERROR,
                        message = "An unexpected error occurred during account creation"
                    }
                });
            }
        }




        /// <summary>
        /// Resends email confirmation code to user
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Confirmation of email sent</returns>
        [HttpPost("resend-confirmation")]
        [Authorize(Policy = "PendingRegistration")]
        public async Task<IActionResult> ResendConfirmationAsync(
            CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();

            _logger.LogInformation("Resend confirmation request received for user: {UserId}", userId);

            try
            {
                var command = new UpdateConfirmEmailTokenCommand
                {
                    UserId = userId,
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Confirmation code resent successfully for user: {UserId}", userId);

                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            email = result.Value.Email,
                            tokenExpiresAt = result.Value.TokenExpiresAt
                        },
                        message = result.Value.Message
                    });
                }

                _logger.LogWarning("Resend confirmation failed for user {UserId}: {Errors}",
                    userId, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during resend confirmation for user: {UserId}", userId);

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.DATABASE_ERROR,
                        message = "An unexpected error occurred while resending confirmation"
                    }
                });
            }
        }




        /// <summary>
        /// Confirms user email with verification code
        /// </summary>
        /// <param name="request">User ID and confirmation code</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Confirmation success message</returns>
        [HttpPost("confirm-email")]
        [Authorize(Policy = "PendingRegistration")]
        public async Task<IActionResult> ConfirmEmailAsync(
            [FromBody] ConfirmEmailRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();

            _logger.LogInformation("Email confirmation request received for user: {UserId}", userId);

            try
            {
                var command = new ConfirmEmailCommand
                {
                    UserId = userId,
                    Code = request.Code?.Trim() ?? string.Empty
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Email confirmed successfully for user: {UserId}", userId);

                    // Set secure HTTP-only cookies
                    SetSecureTemporaryTokenCookie(result.Value.TemporaryAccessToken);

                    return Ok(new
                    {
                        success = true,
                        message = "Email confirmed successfully. You can now complete your account setup."
                    });
                }

                _logger.LogWarning("Email confirmation failed for user {UserId}: {Errors}",
                    userId, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during email confirmation for user: {UserId}", userId);

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.DATABASE_ERROR,
                        message = "An unexpected error occurred during email confirmation"
                    }
                });
            }
        }




        /// <summary>
        /// Completes account setup with username and optional bio
        /// </summary>
        /// <param name="request">Account completion details</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Completed account confirmation</returns>
        [HttpPost("complete-account")]
        [Authorize(Policy = "PendingRegistration")]
        public async Task<IActionResult> CompleteAccountAsync(
            [FromBody] CompleteAccountRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.GetUserId();

            _logger.LogInformation("Account completion request received for user: {UserId}", userId);

            try
            {
                var command = new CompleteAccountCommand
                {
                    UserId = userId,
                    Shortname = request.Shortname?.Trim() ?? string.Empty,
                    Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim()
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    var response = result.Value;
                    _logger.LogInformation("Account setup completed successfully for user: {UserId}", result.Value);

                    // Set secure HTTP-only cookies
                    SetSecureTokenCookies(response.AccessToken, response.RefreshToken);

                    return Ok(new
                    {
                        success = true,
                        message = "Account setup completed successfully. You can now use chat."
                    });
                }

                _logger.LogWarning("Account completion failed for user {UserId}: {Errors}",
                    userId, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during account completion for user: {UserId}", userId);

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.DATABASE_ERROR,
                        message = "An unexpected error occurred during account completion"
                    }
                });
            }
        }




        /// <summary>
        /// Authenticates user and returns access and refresh tokens
        /// </summary>
        /// <param name="request">Login credentials</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>User information and authentication tokens</returns>
        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync(
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Login request received for email: {Email}", request.Email);

            try
            {
                var command = new LoginUserCommand
                {
                    Email = request.Email?.Trim() ?? string.Empty,
                    Password = request.Password ?? string.Empty
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    var response = result.Value;
                    _logger.LogInformation("User logged in successfully: {UserId}", response.UserId);

                    // Set secure HTTP-only cookies
                    SetSecureTokenCookies(response.AccessToken, response.RefreshToken);

                    return Ok(new 
                    {
                        success = true,
                        data = new
                        {
                            Firstname = response.Firstname,
                            Lastname = response.Lastname,
                        },
                    });
                }

                _logger.LogWarning("Login failed for email {Email}: {Errors}",
                    request.Email, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for email: {Email}", request.Email);

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.DATABASE_ERROR,
                        message = "An unexpected error occurred during login"
                    }
                });
            }
        }




        /// <summary>
        /// Refreshes access token using refresh token
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>New access and refresh tokens</returns>
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshTokenAsync(
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Token refresh request received");

            try
            {
                var command = new RefreshTokenCommand
                {
                    RefreshToken = GetRefreshToken(),
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    var response = result.Value;
                    _logger.LogInformation("Token refreshed successfully");

                    // Update secure HTTP-only cookies
                    SetSecureTokenCookies(response.AccessToken, response.RefreshToken);

                    return Ok(new {
                        success = true,
                    });
                }

                _logger.LogWarning("Token refresh failed: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token refresh");

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.DATABASE_ERROR,
                        message = "An unexpected error occurred during token refresh"
                    }
                });
            }
        }




        /// <summary>
        /// Logs out user by clearing authentication cookies
        /// </summary>
        /// <returns>Logout confirmation</returns>
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            _logger.LogInformation("Logout request received");

            try
            {
                var command = new LogoutUserCommand
                {
                    AccessToken = GetAccessToken().Trim(),
                    RefreshToken = GetRefreshToken().Trim(),
                };

                var result = await Mediator.Send(command);

                if (result.IsSuccess)
                {
                    // Clear authentication cookies
                    ClearTokenCookies();

                    _logger.LogInformation("User logged out successfully");

                    return Ok(new
                    {
                        success = true,
                        message = "Logged out successfully"
                    });
                }

                _logger.LogWarning("Token delete failed: {Errors}",
                    string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during logout");

                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.DATABASE_ERROR,
                        message = "An unexpected error occurred during logout"
                    }
                });
            }
        }


        #region Private Helper Methods

        /// <summary>
        /// Get access token from cookies
        /// </summary>
        private string GetAccessToken()
        {
            return HttpContext.Request.Cookies.FirstOrDefault(c => c.Key == "access_token").Value;
        }

        /// <summary>
        /// Get refresh token from cookies
        /// </summary>
        private string GetRefreshToken()
        {
            return HttpContext.Request.Cookies.FirstOrDefault(c => c.Key == "refresh_token").Value;
        }

        /// <summary>
        /// Sets secure HTTP-only cookie for temporary token
        /// </summary>
        private void SetSecureTemporaryTokenCookie(string tempToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddMinutes(60),
            };

            SetCookie("access_token", tempToken, cookieOptions);
        }

        /// <summary>
        /// Sets secure HTTP-only cookies for authentication tokens
        /// </summary>
        private void SetSecureTokenCookies(string accessToken, string refreshToken)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTime.UtcNow.AddDays(6),
            };

            SetCookie("access_token", accessToken, cookieOptions);
            SetCookie("refresh_token", refreshToken, cookieOptions);
        }

        /// <summary>
        /// Clears authentication token cookies
        /// </summary>
        private void ClearTokenCookies()
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(-1)
            };

            SetCookie("access_token", string.Empty, cookieOptions);
            SetCookie("refresh_token", string.Empty, cookieOptions);
        }

        #endregion
    }
}
