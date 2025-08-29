using Domain.Common;
using Domain.Common.Constants;
using Domain.Entities;
using Domain.Errors;
using Domain.Repositories;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Application.CQRS.Users.Commands.UpdateShortname
{
    public class UpdateShortnameCommand : IRequest<Result<UpdateShortnameCommandResponse>>
    {
        public Guid UserId { get; set; }
        public string Shortname { get; set; }
    }

    public class UpdateShortnameCommandResponse
    {
        public string NewShortname { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }

    public class UpdateShortnameCommandValidator : AbstractValidator<UpdateShortnameCommand>
    {
        public UpdateShortnameCommandValidator()
        {
            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("UserId is required");

            RuleFor(x => x.Shortname)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("Username is required")
                .MinimumLength(ShortnameConstants.MinLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_SHORT)
                    .WithMessage($"Username must be at least {ShortnameConstants.MinLength} characters long")
                .MaximumLength(ShortnameConstants.MaxLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_LONG)
                    .WithMessage($"Username cannot exceed {ShortnameConstants.MaxLength} characters")
                .Matches(@"^[a-zA-Z0-9_.-]+$")
                    .WithErrorCode(ErrorCodes.INVALID_FORMAT)
                    .WithMessage("Username can only contain letters, numbers, underscore, dot, and hyphen")
                .Must(BeValidUsernameFormat)
                    .WithErrorCode(ErrorCodes.INVALID_FORMAT)
                    .WithMessage("Username cannot start or end with special characters");
        }

        private bool BeValidUsernameFormat(string shortname)
        {
            if (string.IsNullOrEmpty(shortname)) return false;

            // Cannot start or end with special characters
            return !Regex.IsMatch(shortname, @"^[._-]|[._-]$") &&
                   // Cannot contain consecutive special characters
                   !Regex.IsMatch(shortname, @"[._-]{2,}");
        }
    }

    public class UpdateShortnameCommandHandler : IRequestHandler<UpdateShortnameCommand, Result<UpdateShortnameCommandResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdateShortnameCommandHandler> _logger;

        public UpdateShortnameCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<UpdateShortnameCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<UpdateShortnameCommandResponse>> Handle(UpdateShortnameCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting username update process for user: {UserId}", request.UserId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);

            try
            {
                var userResult = await GetAndValidateUserAsync(request.UserId, cancellationToken);
                if (userResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return userResult.ToResult();
                }

                var user = userResult.Value;

                var shortnameValidationResult = await ValidateShortnameChangeAsync(user, request.Shortname, cancellationToken);
                if (shortnameValidationResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return shortnameValidationResult;
                }

                await UpdateUserShortnameAsync(user, request.Shortname, cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Username update successful for user: {UserId}, new username: {Username}",
                    user.Id, request.Shortname);

                return Result.Ok(new UpdateShortnameCommandResponse
                {
                    NewShortname = request.Shortname.Trim().ToLowerInvariant(),
                    UpdatedAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Username update failed for user: {UserId}", request.UserId);

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during username update");
                }

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to update username due to system error"
                ));
            }
        }

        private async Task<Result<User>> GetAndValidateUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving user by ID: {UserId}", userId);

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Username update failed - user not found: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_FOUND,
                    "User not found",
                    new { UserId = userId }
                ));
            }

            if (!user.IsVerified)
            {
                _logger.LogWarning("Username update failed - user not verified: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_VERIFIED,
                    "User account must be verified before updating username",
                    new
                    {
                        UserId = userId,
                        SuggestedAction = "Complete your account verification first"
                    }
                ));
            }

            if (user.IsDeleted)
            {
                _logger.LogWarning("Username update failed - user is deleted: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_DELETED,
                    "Cannot update username for deleted user account"
                ));
            }

            _logger.LogDebug("User validation successful: {UserId}", userId);
            return Result.Ok(user);
        }

        private async Task<Result> ValidateShortnameChangeAsync(User user, string newShortname, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Validating username change for user: {UserId}, new username: {Username}", user.Id, newShortname);

            // Get current user mention
            var currentMention = await _unitOfWork.UserMentionRepository.GetByUserIdAsync(user.Id, cancellationToken);

            if (currentMention == null)
            {
                _logger.LogWarning("Username update failed - user mention not found: {UserId}", user.Id);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_MENTION_NOT_FOUND,
                    "User mention not found"
                ));
            }

            var normalizedNewShortname = newShortname.Trim().ToLowerInvariant();
            var normalizedCurrentShortname = currentMention.Shortname.ToLowerInvariant();

            // Check if trying to set the same username
            if (normalizedNewShortname == normalizedCurrentShortname)
            {
                _logger.LogWarning("Username update failed - same username provided: {Username}", newShortname);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USERNAME_UNCHANGED,
                    "The new username is the same as your current username",
                    new
                    {
                        CurrentUsername = currentMention.Shortname,
                        RequestedUsername = newShortname,
                        SuggestedAction = "Choose a different username"
                    }
                ));
            }

            // Check if username is already taken
            var isAvailable = await _unitOfWork.MentionRepository.IsShortnameAvailableAsync(
                normalizedNewShortname,
                currentMention.Id,
                cancellationToken);

            if (!isAvailable)
            {
                _logger.LogWarning("Username update failed - username already taken: {Username}", newShortname);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USERNAME_ALREADY_TAKEN,
                    "This username is already taken",
                    new
                    {
                        RequestedUsername = newShortname,
                        SuggestedAction = "Choose a different username"
                    }
                ));
            }

            _logger.LogDebug("Username validation successful: {Username}", newShortname);
            return Result.Ok();
        }

        private async Task UpdateUserShortnameAsync(User user, string newShortname, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Updating username for user: {UserId}", user.Id);

            var userMention = await _unitOfWork.UserMentionRepository.GetByUserIdAsync(user.Id, cancellationToken);

            if (userMention == null)
            {
                throw new InvalidOperationException($"User mention not found for user: {user.Id}");
            }

            userMention.Shortname = newShortname.Trim().ToLowerInvariant();

            await _unitOfWork.UserMentionRepository.UpdateAsync(userMention, cancellationToken);

            _logger.LogDebug("Saving changes to database");
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Username updated successfully for user: {UserId}", user.Id);
        }
    }
}
