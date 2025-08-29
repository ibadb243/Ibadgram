using Application.Interfaces.Services;
using Domain.Common;
using Domain.Common.Constants;
using Domain.Entities;
using Domain.Errors;
using Domain.Repositories;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Users.Commands.ConfirmEmail
{
    public class ConfirmEmailCommand : IRequest<Result<ConfirmEmailCommandResponse>>
    {
        public Guid UserId { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    public class ConfirmEmailCommandResponse
    {
        public string TemporaryAccessToken { get; set; } = string.Empty;
    }

    public class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
    {
        public ConfirmEmailCommandValidator()
        {
            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("UserId is required");

            RuleFor(x => x.Code)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("Code is required")
                .Length(EmailConstants.EmailConfirmationTokenLength)
                    .WithErrorCode(ErrorCodes.INVALID_FORMAT)
                    .WithMessage($"Confirmation code must be {EmailConstants.EmailConfirmationTokenLength} characters long")
                .Matches("^[0-9A-Fa-f]+$")
                    .WithErrorCode(ErrorCodes.INVALID_FORMAT)
                    .WithMessage("Confirmation code contains invalid characters");
        }
    }

    public class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, Result<ConfirmEmailCommandResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;
        private readonly ILogger<ConfirmEmailCommandHandler> _logger;

        public ConfirmEmailCommandHandler(
            IUnitOfWork unitOfWork,
            ITokenService tokenService,
            ILogger<ConfirmEmailCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<Result<ConfirmEmailCommandResponse>> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting email confirmation process for user: {UserId}", request.UserId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);

            try
            {
                var userResult = await GetUserAsync(request.UserId, cancellationToken);
                if (userResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return userResult.ToResult();
                }

                var user = userResult.Value;

                var validationResult = ValidateConfirmationStatus(user);
                if (validationResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return validationResult;
                }

                var codeValidationResult = ValidateConfirmationCode(user, request.Code);
                if (codeValidationResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return codeValidationResult;
                }

                await UpdateUserConfirmationStatus(user, cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Email confirmation completed successfully for user: {UserId}", user.Id);

                var tempAccessToken = _tokenService.GenerateTemporaryToken(user.Id, "pc");

                return Result.Ok(new ConfirmEmailCommandResponse
                {
                    TemporaryAccessToken = tempAccessToken,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email confirmation failed for user: {UserId}", request.UserId);

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during email confirmation");
                }

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to confirm email due to system error"
                ));
            }
        }

        private async Task<Result<User>> GetUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving user by ID: {UserId}", userId);

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Email confirmation failed - user not found: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_FOUND,
                    "User not found",
                    new { UserId = userId }
                ));
            }

            _logger.LogDebug("User found: {UserId}, Email: {Email}", user.Id, user.Email);
            return Result.Ok(user);
        }

        private Result ValidateConfirmationStatus(User user)
        {
            _logger.LogDebug("Checking email confirmation status for user: {UserId}", user.Id);

            if (user.EmailConfirmed)
            {
                _logger.LogWarning("Email confirmation failed - email already confirmed for user: {UserId}", user.Id);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.EMAIL_ALREADY_CONFIRMED,
                    "Email address is already confirmed",
                    new
                    {
                        UserId = user.Id,
                        Email = user.Email,
                        NextAction = "You can proceed to complete your account setup"
                    }
                ));
            }

            if (string.IsNullOrEmpty(user.EmailConfirmationToken))
            {
                _logger.LogCritical("Email confirmation failed - no token found for user: {UserId}", user.Id);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CONFIRMATION_TOKEN_NOT_FOUND,
                    "No confirmation code found for this user",
                    new
                    {
                        UserId = user.Id,
                        SuggestedAction = "Request a new confirmation code"
                    }
                ));
            }

            return Result.Ok();
        }

        private Result ValidateConfirmationCode(User user, string providedCode)
        {
            _logger.LogDebug("Validating confirmation code for user: {UserId}", user.Id);

            if (user.EmailConfirmationTokenExpiry.HasValue &&
                user.EmailConfirmationTokenExpiry.Value < DateTime.UtcNow)
            {
                _logger.LogWarning("Confirmation code expired for user: {UserId}, expired at: {ExpiredAt}",
                    user.Id, user.EmailConfirmationTokenExpiry.Value);

                var minutesExpired = (int)(DateTime.UtcNow - user.EmailConfirmationTokenExpiry.Value).TotalMinutes;

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CONFIRMATION_CODE_EXPIRED,
                    "Confirmation code has expired",
                    new
                    {
                        UserId = user.Id,
                        ExpiredAt = user.EmailConfirmationTokenExpiry.Value,
                        MinutesExpired = minutesExpired,
                        CanRequestNew = true,
                        SuggestedAction = "Request a new confirmation code"
                    }
                ));
            }

            if (!string.Equals(user.EmailConfirmationToken, providedCode, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid confirmation code provided for user: {UserId}", user.Id);

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.INVALID_CONFIRMATION_CODE,
                    "Invalid confirmation code",
                    new
                    {
                        UserId = user.Id,
                        RemainingTime = user.EmailConfirmationTokenExpiry?.Subtract(DateTime.UtcNow).TotalMinutes,
                        SuggestedAction = "Check your email and enter the correct code"
                    }
                ));
            }

            _logger.LogDebug("Confirmation code validated successfully for user: {UserId}", user.Id);
            return Result.Ok();
        }

        private async Task UpdateUserConfirmationStatus(User user, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Updating user confirmation status: {UserId}", user.Id);

            user.EmailConfirmed = true;
            user.EmailConfirmationToken = null;
            user.EmailConfirmationTokenExpiry = null;

            await _unitOfWork.UserRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("User confirmation status updated successfully: {UserId}", user.Id);
        }
    }
}
