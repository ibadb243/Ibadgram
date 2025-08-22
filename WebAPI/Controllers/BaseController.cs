using Domain.Common;
using FluentResults;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace WebAPI.Controllers
{
    [ApiController]
    public abstract class BaseController : ControllerBase
    {
        private IMediator _mediator;
        protected IMediator Mediator => _mediator ??= HttpContext.RequestServices.GetService<IMediator>()!;

        protected BaseController(IMediator mediator)
        {
            _mediator = mediator;
        }

        /// <summary>
        /// Sets cookie
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value</param>
        /// <param name="options">Cookie options</param>
        protected void SetCookie(string key, string value, CookieOptions options = null)
        {
            options ??= new CookieOptions()
            {
                Secure = true,
                HttpOnly = false,
                SameSite = SameSiteMode.Strict
            };

            HttpContext.Response.Cookies.Append(key, value, options);
        }


        /// <summary>
        /// Set cookies with one cookie configuration for all them
        /// </summary>
        /// <param name="options">Cookie options</param>
        /// <param name="pairs">Key-Value pairs</param>
        protected void SetCookies(CookieOptions options = null, params(string key, string value)[] pairs)
        {
            foreach (var pair in pairs)
                SetCookie(pair.key, pair.value, options);
        }


        /// <summary>
        /// Set cookies
        /// </summary>
        /// <param name="pairs">Key-Value-Cookie options pairs</param>
        protected void SetCookies(params (string key, string value, CookieOptions options)[] pairs)
        {
            foreach (var pair in pairs)
                SetCookie(pair.key, pair.value, pair.options);
        }


        /// <summary>
        /// Handles business logic errors with appropriate HTTP status codes
        /// </summary>
        protected IActionResult HandleBusinessErrors(IReadOnlyList<IError> errors)
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
                        ErrorCodes.CHAT_NOT_FOUND => NotFound(errorResponse),

                        // Bad Request Errors (400)
                        ErrorCodes.EMAIL_NOT_CONFIRMED => BadRequest(errorResponse),
                        ErrorCodes.CONFIRMATION_CODE_EXPIRED => BadRequest(errorResponse),
                        ErrorCodes.INVALID_CONFIRMATION_CODE => BadRequest(errorResponse),
                        ErrorCodes.REFRESH_TOKEN_EXPIRED => BadRequest(errorResponse),
                        ErrorCodes.REFRESH_TOKEN_REVOKED => BadRequest(errorResponse),
                        ErrorCodes.USER_NOT_VERIFIED => BadRequest(errorResponse),
                        ErrorCodes.INVALID_FORMAT => BadRequest(errorResponse),
                        ErrorCodes.INVALID_CONTENT => BadRequest(errorResponse),
                        ErrorCodes.REQUEST_EMTPY => BadRequest(errorResponse),

                        // Unauthorized Errors (401)
                        ErrorCodes.INVALID_CREDENTIALS => Unauthorized(errorResponse),
                        ErrorCodes.CHAT_ACCESS_DENIED => Unauthorized(errorResponse),

                        // Conflict Errors (409)
                        ErrorCodes.USER_ALREADY_VERIFIED => Conflict(errorResponse),
                        ErrorCodes.EMAIL_ALREADY_CONFIRMED => Conflict(errorResponse),
                        ErrorCodes.EMAIL_AWAITING_CONFIRMATION => Conflict(errorResponse),
                        ErrorCodes.ACCOUNT_ALREADY_COMPLETED => Conflict(errorResponse),
                        ErrorCodes.USERNAME_ALREADY_TAKEN => Conflict(errorResponse),
                        ErrorCodes.CHAT_ALREADY_EXISTS => Conflict(errorResponse),

                        // Gone Errors (410)
                        ErrorCodes.USER_DELETED => StatusCode(410, errorResponse),
                        ErrorCodes.CHAT_DELETED => StatusCode(410, errorResponse),

                        // Service Unavailable Errors (503)
                        ErrorCodes.EMAIL_DELIVERY_FAILED => StatusCode(503, errorResponse),
                        ErrorCodes.EXTERNAL_SERVICE_ERROR => StatusCode(503, errorResponse),

                        // Internal Server Errors (500)
                        ErrorCodes.DATABASE_ERROR => StatusCode(500, errorResponse),

                        // Default to Bad Request for unknown business errors
                        _ => HandleValidationErrors(errors)
                    };
                }
            }

            // Handle validation errors (FluentValidation) when no business error code is found
            return HandleValidationErrors(errors);
        }


        /// <summary>
        /// Handles FluentValidation errors
        /// </summary>
        protected IActionResult HandleValidationErrors(IReadOnlyList<IError> errors)
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
    }
}
