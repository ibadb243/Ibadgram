using Application.CQRS.Users.Commands.CompleteAccount;
using Application.CQRS.Users.Commands.ConfirmEmail;
using Application.CQRS.Users.Commands.CreateAccount;
using Application.CQRS.Users.Commands.Login;
using Application.CQRS.Users.Commands.Refresh;
using Application.CQRS.Users.Commands.UpdateConfirmEmailToken;
using Domain.Common;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using WebAPI.Models.DTOs.Auth;

namespace WebAPI.Controllers
{

    [ApiController]
    [Route("api/auth/")]
    public class AuthController : BaseController
    {
        private readonly ILogger<AuthController> _logger;

        public AuthController(IMediator mediator, ILogger<AuthController> logger)
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

                    return Ok(new
                    {
                        success = true,
                        data = new { userId = result.Value },
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
        /// <param name="request">User ID for confirmation resend</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Confirmation of email sent</returns>
        [HttpPost("resend-confirmation")]
        public async Task<IActionResult> ResendConfirmationAsync(
            [FromBody] ResendConfirmationRequest request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Resend confirmation request received for user: {UserId}", request.UserId);

            try
            {
                var command = new UpdateConfirmEmailTokenCommand
                {
                    UserId = request.UserId
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Confirmation code resent successfully for user: {UserId}", request.UserId);

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
                    request.UserId, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during resend confirmation for user: {UserId}", request.UserId);

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
        public async Task<IActionResult> ConfirmEmailAsync(
            [FromBody] ConfirmEmailRequest request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Email confirmation request received for user: {UserId}", request.UserId);

            try
            {
                var command = new ConfirmEmailCommand
                {
                    UserId = request.UserId,
                    Code = request.Code?.Trim() ?? string.Empty
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Email confirmed successfully for user: {UserId}", request.UserId);

                    return Ok(new
                    {
                        success = true,
                        message = "Email confirmed successfully. You can now complete your account setup."
                    });
                }

                _logger.LogWarning("Email confirmation failed for user {UserId}: {Errors}",
                    request.UserId, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during email confirmation for user: {UserId}", request.UserId);

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
        public async Task<IActionResult> CompleteAccountAsync(
            [FromBody] CompleteAccountRequest request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Account completion request received for user: {UserId}", request.UserId);

            try
            {
                var command = new CompleteAccountCommand
                {
                    UserId = request.UserId,
                    Shortname = request.Shortname?.Trim() ?? string.Empty,
                    Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim()
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Account setup completed successfully for user: {UserId}", result.Value);

                    return Ok(new
                    {
                        success = true,
                        data = new { userId = result.Value },
                        message = "Account setup completed successfully. You can now log in."
                    });
                }

                _logger.LogWarning("Account completion failed for user {UserId}: {Errors}",
                    request.UserId, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleBusinessErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during account completion for user: {UserId}", request.UserId);

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

                    return Ok(new LoginResponse
                    {
                        UserId = response.UserId,
                        Firstname = response.Firstname,
                        Lastname = response.Lastname,
                        Bio = response.Bio,
                        AccessToken = response.AccessToken,
                        RefreshToken = response.RefreshToken
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
        /// <param name="request">Refresh token</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>New access and refresh tokens</returns>
        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshTokenAsync(
            [FromBody] RefreshTokenRequest request,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Token refresh request received");

            try
            {
                var command = new RefreshTokenCommand
                {
                    RefreshToken = request.RefreshToken?.Trim() ?? string.Empty
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    var response = result.Value;
                    _logger.LogInformation("Token refreshed successfully");

                    // Update secure HTTP-only cookies
                    SetSecureTokenCookies(response.AccessToken, response.RefreshToken);

                    return Ok(new Models.DTOs.Auth.RefreshTokenResponse
                    {
                        AccessToken = response.AccessToken,
                        RefreshToken = response.RefreshToken
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
        public IActionResult Logout()
        {
            _logger.LogInformation("Logout request received");

            try
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
        /// Handles business logic errors with appropriate HTTP status codes
        /// </summary>
        private IActionResult HandleBusinessErrors(IReadOnlyList<IError> errors)
        {
            foreach (var error in errors)
            {
                if (error.Metadata.TryGetValue("ErrorCode", out var code))
                {
                    var errorCode = code.ToString();
                    var additionalData = error.Metadata.TryGetValue("AdditionalData", out var data) ? data : null;

                    var errorResponse = new
                    {
                        success = false,
                        error = new
                        {
                            code = errorCode,
                            message = error.Message,
                            additional_data = additionalData
                        }
                    };

                    return errorCode switch
                    {
                        // Not Found Errors (404)
                        ErrorCodes.USER_NOT_FOUND => NotFound(errorResponse),
                        ErrorCodes.CONFIRMATION_TOKEN_NOT_FOUND => NotFound(errorResponse),
                        ErrorCodes.REFRESH_TOKEN_NOT_FOUND => NotFound(errorResponse),

                        // Bad Request Errors (400)
                        ErrorCodes.EMAIL_NOT_CONFIRMED => BadRequest(errorResponse),
                        ErrorCodes.CONFIRMATION_CODE_EXPIRED => BadRequest(errorResponse),
                        ErrorCodes.INVALID_CONFIRMATION_CODE => BadRequest(errorResponse),
                        ErrorCodes.REFRESH_TOKEN_EXPIRED => BadRequest(errorResponse),
                        ErrorCodes.REFRESH_TOKEN_REVOKED => BadRequest(errorResponse),

                        // Unauthorized Errors (401)
                        ErrorCodes.INVALID_CREDENTIALS => Unauthorized(errorResponse),
                        ErrorCodes.USER_NOT_VERIFIED => Unauthorized(errorResponse),

                        // Conflict Errors (409)
                        ErrorCodes.USER_ALREADY_VERIFIED => Conflict(errorResponse),
                        ErrorCodes.EMAIL_ALREADY_CONFIRMED => Conflict(errorResponse),
                        ErrorCodes.EMAIL_AWAITING_CONFIRMATION => Conflict(errorResponse),
                        ErrorCodes.ACCOUNT_ALREADY_COMPLETED => Conflict(errorResponse),
                        ErrorCodes.USERNAME_ALREADY_TAKEN => Conflict(errorResponse),

                        // Gone Errors (410)
                        ErrorCodes.USER_DELETED => StatusCode(410, errorResponse),

                        // Internal Server Errors (500)
                        ErrorCodes.DATABASE_ERROR => StatusCode(500, errorResponse),

                        // Default to Bad Request for validation errors
                        _ => BadRequest(errorResponse)
                    };
                }
            }

            // Handle validation errors (FluentValidation)
            return HandleValidationErrors(errors);
        }

        /// <summary>
        /// Handles FluentValidation errors
        /// </summary>
        private IActionResult HandleValidationErrors(IReadOnlyList<IError> errors)
        {
            var validationErrors = new Dictionary<string, object>();

            foreach (var error in errors)
            {
                var errorCode = error.Metadata.TryGetValue("ErrorCode", out var code) ? code.ToString() : "VALIDATION_ERROR";
                var propertyName = error.Metadata.TryGetValue("PropertyName", out var prop) ? prop.ToString()?.ToLowerInvariant() : "unknown";
                var attemptedValue = error.Metadata.TryGetValue("AttemptedValue", out var value) ? value : null;

                validationErrors[propertyName ?? "unknown"] = new
                {
                    code = errorCode,
                    message = error.Message,
                    attempted_value = attemptedValue
                };
            }

            return BadRequest(new
            {
                success = false,
                errors = validationErrors
            });
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
            };

            HttpContext.Response.Cookies.Append("access_token", accessToken, cookieOptions);
            HttpContext.Response.Cookies.Append("refresh_token", refreshToken, cookieOptions);
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

            HttpContext.Response.Cookies.Append("access_token", string.Empty, cookieOptions);
            HttpContext.Response.Cookies.Append("refresh_token", string.Empty, cookieOptions);
        }

        #endregion
    }
}
