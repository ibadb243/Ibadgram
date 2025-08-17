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
        /// Creates a new user account
        /// </summary>
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
                    Firstname = request.Firstname,
                    Lastname = request.Lastname,
                    Email = request.Email,
                    Password = request.Password
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    _logger.LogInformation("Account created successfully with ID: {UserId}", result.Value);
                    return Ok(new
                    {
                        success = true,
                        data = new { userId = result.Value },
                        message = "Account created successfully. Please check your email for confirmation."
                    });
                }
                
                _logger.LogWarning("Account creation failed: {Errors}", string.Join(", ", result.Errors.Select(e => e.Message)));
                return HandleErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during account creation for email: {Email}", request.Email);
                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.EXTERNAL_SERVICE_ERROR,
                        message = "An unexpected error occurred during account creation"
                    }
                });
            }
        }

        /// <summary>
        /// Resends email confirmation code to user
        /// </summary>
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
                        data = result.Value,
                    });
                }

                _logger.LogWarning("Resend confirmation failed for user {UserId}: {Errors}", request.UserId, string.Join(", ", result.Errors.Select(e => e.Message)));

                return HandleResendConfirmationErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during resend confirmation for email: {Email}", request.Email);
                return StatusCode(500, new { message = "An unexpected error occurred while resending confirmation" });
            }
        }

        /// <summary>
        /// Confirms user email with verification code
        /// </summary>
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
                    Code = request.Code
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

                return HandleConfirmEmailErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during email confirmation for user: {UserId}", request.UserId);
                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.EXTERNAL_SERVICE_ERROR,
                        message = "An unexpected error occurred during email confirmation"
                    }
                });
            }
        }

        /// <summary>
        /// Completes account setup with shortname and bio
        /// </summary>
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
                    Shortname = request.Shortname.Trim(),
                    Bio = request.Bio?.Trim()
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

                return HandleCompleteAccountErrors(result.Errors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during account completion for user: {UserId}", request.UserId);
                return StatusCode(500, new
                {
                    success = false,
                    error = new
                    {
                        code = ErrorCodes.EXTERNAL_SERVICE_ERROR,
                        message = "An unexpected error occurred during account completion"
                    }
                });
            }
        }

        /// <summary>
        /// Authenticates user and returns access and refresh tokens
        /// </summary>
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
                    Email = request.Email,
                    Password = request.Password
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    var response = result.Value;
                    _logger.LogInformation("User logged in successfully: {UserId}", response.UserId);

                    this.HttpContext.Response.Cookies.Append("access_token", response.AccessToken);
                    this.HttpContext.Response.Cookies.Append("refresh_token", response.RefreshToken);

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
                return BadRequest(new { errors = result.Errors.ToDictionary(k => ((string)k.Metadata["PropertyName"]).ToLower(), v => v.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during login for email: {Email}", request.Email);
                return StatusCode(500, new { message = "An unexpected error occurred during login" });
            }
        }

        /// <summary>
        /// Refreshes access token using refresh token
        /// </summary>
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
                    RefreshToken = request.RefreshToken
                };

                var result = await Mediator.Send(command, cancellationToken);

                if (result.IsSuccess)
                {
                    var response = result.Value;
                    _logger.LogInformation("Token refreshed successfully");

                    this.HttpContext.Response.Cookies.Append("access_token", response.AccessToken);
                    this.HttpContext.Response.Cookies.Append("refresh_token", response.RefreshToken);

                    return Ok(new Models.DTOs.Auth.RefreshTokenResponse
                    {
                        AccessToken = response.AccessToken,
                        RefreshToken = response.RefreshToken
                    });
                }

                _logger.LogWarning("Token refresh failed: {Errors}", string.Join(", ", result.Errors.Select(e => e.Message)));
                return BadRequest(new { errors = result.Errors.ToDictionary(k => ((string)k.Metadata["PropertyName"]).ToLower(), v => v.Message) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token refresh");
                return StatusCode(500, new { message = "An unexpected error occurred during token refresh" });
            }
        }

        private IActionResult HandleResendConfirmationErrors(IReadOnlyList<IError> errors)
        {
            // ...

            // Fallback for validation errors
            return HandleErrors(errors);
        }

        private IActionResult HandleCompleteAccountErrors(IReadOnlyList<IError> errors)
        {
            foreach (var error in errors)
            {
                if (error.Metadata.TryGetValue("ErrorCode", out var code))
                {
                    var errorCode = code.ToString();
                    var additionalData = error.Metadata.TryGetValue("AdditionalData", out var data) ? data : null;

                    return errorCode switch
                    {
                        ErrorCodes.USER_NOT_FOUND => NotFound(new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message,
                                additional_data = additionalData
                            }
                        }),

                        ErrorCodes.EMAIL_NOT_CONFIRMED => BadRequest(new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message,
                                additional_data = additionalData
                            }
                        }),

                        ErrorCodes.ACCOUNT_ALREADY_COMPLETED => Conflict(new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message,
                                additional_data = additionalData
                            }
                        }),

                        ErrorCodes.USERNAME_ALREADY_TAKEN => Conflict(new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message,
                                additional_data = additionalData
                            }
                        }),

                        _ => StatusCode(500, new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message
                            }
                        })
                    };
                }
            }

            // Fallback for validation errors
            return HandleErrors(errors);
        }

        private IActionResult HandleConfirmEmailErrors(IReadOnlyList<IError> errors)
        {
            foreach (var error in errors)
            {
                if (error.Metadata.TryGetValue("ErrorCode", out var code))
                {
                    var errorCode = code.ToString();
                    var additionalData = error.Metadata.TryGetValue("AdditionalData", out var data) ? data : null;

                    return errorCode switch
                    {
                        ErrorCodes.USER_NOT_FOUND => NotFound(new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message,
                                additional_data = additionalData
                            }
                        }),

                        ErrorCodes.EMAIL_ALREADY_CONFIRMED => Conflict(new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message,
                                additional_data = additionalData
                            }
                        }),

                        ErrorCodes.CONFIRMATION_CODE_EXPIRED => BadRequest(new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message,
                                additional_data = additionalData
                            }
                        }),

                        ErrorCodes.INVALID_CONFIRMATION_CODE => BadRequest(new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message,
                                additional_data = additionalData
                            }
                        }),

                        _ => StatusCode(500, new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message
                            }
                        })
                    };
                }
            }

            // Fallback for validation errors
            return HandleErrors(errors);
        }

        private IActionResult HandleErrors(IReadOnlyList<IError> errors)
        {
            var response = new
            {
                success = false,
                errors = new Dictionary<string, object>()
            };

            foreach (var error in errors)
            {
                var errorCode = error.Metadata.TryGetValue("ErrorCode", out var code) ? code.ToString() : "UNKNOWN_ERROR";

                if (error.Metadata.TryGetValue("PropertyName", out var propertyName))
                {
                    // Validation error
                    response.errors[propertyName.ToString().ToLower()] = new
                    {
                        code = errorCode,
                        message = error.Message,
                        attempted_value = error.Metadata.TryGetValue("AttemptedValue", out var value) ? value : null
                    };
                }
                else
                {
                    // Business logic error
                    var additionalData = error.Metadata.TryGetValue("AdditionalData", out var data) ? data : null;

                    return errorCode switch
                    {
                        ErrorCodes.USER_ALREADY_VERIFIED => Conflict(new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message,
                                additional_data = additionalData
                            }
                        }),
                        ErrorCodes.EMAIL_ALREADY_CONFIRMED => Conflict(new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message,
                                additional_data = additionalData
                            }
                        }),
                        ErrorCodes.EMAIL_AWAITING_CONFIRMATION => Conflict(new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message,
                                additional_data = additionalData
                            }
                        }),
                        _ => StatusCode(500, new
                        {
                            success = false,
                            error = new
                            {
                                code = errorCode,
                                message = error.Message,
                                additional_data = additionalData
                            }
                        })
                    };
                }
            }

            return BadRequest(response);
        }
    }
}
