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

namespace Application.CQRS.Users.Commands.UpdateUser
{
    public class UpdateUserCommand : IRequest<Result<UpdateUserCommandResponse>>
    {
        public Guid UserId { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Bio { get; set; }
    }

    public class UpdateUserCommandResponse
    {
        public Guid UserId { get; set; }
        public string Firstname { get; set; } = string.Empty;
        public string? Lastname { get; set; }
        public string? Bio { get; set; }
        public DateTime UpdatedAt { get; set; }
        public UpdatedFields ChangedFields { get; set; } = new();
    }

    public class UpdatedFields
    {
        public bool FirstnameChanged { get; set; }
        public bool LastnameChanged { get; set; }
        public bool BioChanged { get; set; }
    }

    public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
    {
        public UpdateUserCommandValidator()
        {
            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("UserId is required");

            RuleFor(x => x.Firstname)
                .Cascade(CascadeMode.Stop)
                .MinimumLength(UserConstants.FirstnameMinLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_SHORT)
                    .WithMessage($"Firstname must be at least {UserConstants.FirstnameMinLength} characters long")
                .MaximumLength(UserConstants.FirstnameMaxLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_LONG)
                    .WithMessage($"Firstname cannot exceed {UserConstants.FirstnameMaxLength} characters")
                .Must(BeValidName)
                    .WithErrorCode(ErrorCodes.INVALID_FORMAT)
                    .WithMessage("Firstname contains invalid characters")
                .When(x => !string.IsNullOrEmpty(x.Firstname));

            RuleFor(x => x.Lastname)
                .Cascade(CascadeMode.Stop)
                .MaximumLength(UserConstants.LastnameLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_LONG)
                    .WithMessage($"Lastname cannot exceed {UserConstants.LastnameLength} characters")
                .Must(BeValidName)
                    .WithErrorCode(ErrorCodes.INVALID_FORMAT)
                    .WithMessage("Lastname contains invalid characters")
                .When(x => !string.IsNullOrEmpty(x.Lastname));

            RuleFor(x => x.Bio)
                .Cascade(CascadeMode.Stop)
                .MaximumLength(UserConstants.BioLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_LONG)
                    .WithMessage($"Bio cannot exceed {UserConstants.BioLength} characters")
                .Must(BeValidBioContent)
                    .WithErrorCode(ErrorCodes.INVALID_CONTENT)
                    .WithMessage("Bio contains inappropriate content")
                .When(x => !string.IsNullOrEmpty(x.Bio));

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .Must(HaveAtLeastOneField)
                    .WithErrorCode(ErrorCodes.REQUEST_EMTPY)
                    .WithMessage("At least one field must be provided for update")
                    .OverridePropertyName("Request");
        }

        private bool HaveAtLeastOneField(UpdateUserCommand command)
        {
            return !string.IsNullOrEmpty(command.Firstname) ||
                   !string.IsNullOrEmpty(command.Lastname) ||
                   !string.IsNullOrEmpty(command.Bio);
        }

        private bool BeValidName(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;

            // Allow letters, spaces, hyphens, apostrophes
            return Regex.IsMatch(name.Trim(), @"^[a-zA-ZÀ-ÿ\s'-]+$") &&
                   // No consecutive spaces or special chars
                   !Regex.IsMatch(name.Trim(), @"[\s'-]{2,}") &&
                   // No leading/trailing special chars
                   !Regex.IsMatch(name.Trim(), @"^[\s'-]|[\s'-]$");
        }

        private bool BeValidBioContent(string bio)
        {
            if (string.IsNullOrEmpty(bio)) return true;

            var forbiddenPatterns = new[]
            {
                @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", // XSS
                @"javascript:", // JavaScript URLs
                @"data:text\/html", // Data URLs
                @"<iframe\b[^>]*>.*?<\/iframe>", // Iframe tags
                @"<object\b[^>]*>.*?<\/object>", // Object tags
            };

            return !forbiddenPatterns.Any(pattern =>
                Regex.IsMatch(bio, pattern, RegexOptions.IgnoreCase));
        }
    }

    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result<UpdateUserCommandResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdateUserCommandHandler> _logger;

        public UpdateUserCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<UpdateUserCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<UpdateUserCommandResponse>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting user profile update process for user: {UserId}", request.UserId);

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

                var updateResult = await UpdateUserProfileAsync(user, request, cancellationToken);
                if (updateResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return updateResult;
                }

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("User profile update successful for user: {UserId}", user.Id);

                return updateResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User profile update failed for user: {UserId}", request.UserId);

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during user profile update");
                }

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to update user profile due to system error"
                ));
            }
        }

        private async Task<Result<User>> GetAndValidateUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving user by ID: {UserId}", userId);

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("User profile update failed - user not found: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_FOUND,
                    "User not found",
                    new { UserId = userId }
                ));
            }

            if (!user.IsVerified)
            {
                _logger.LogWarning("User profile update failed - user not verified: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_VERIFIED,
                    "User account must be verified before updating profile",
                    new
                    {
                        UserId = userId,
                        SuggestedAction = "Complete your account verification first"
                    }
                ));
            }

            if (user.IsDeleted)
            {
                _logger.LogWarning("User profile update failed - user is deleted: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_DELETED,
                    "Cannot update profile for deleted user account"
                ));
            }

            _logger.LogDebug("User validation successful: {UserId}", userId);
            return Result.Ok(user);
        }

        private async Task<Result<UpdateUserCommandResponse>> UpdateUserProfileAsync(User user, UpdateUserCommand request, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Updating user profile for user: {UserId}", user.Id);

            var changes = new UpdatedFields();
            var originalFirstname = user.Firstname;
            var originalLastname = user.Lastname;
            var originalBio = user.Bio;

            // Update firstname if provided and different
            if (!string.IsNullOrEmpty(request.Firstname))
            {
                var newFirstname = request.Firstname.Trim();
                if (newFirstname != user.Firstname)
                {
                    user.Firstname = newFirstname;
                    changes.FirstnameChanged = true;
                }
            }

            // Update lastname if provided and different
            if (!string.IsNullOrEmpty(request.Lastname))
            {
                var newLastname = request.Lastname.Trim();
                if (newLastname != user.Lastname)
                {
                    user.Lastname = newLastname;
                    changes.LastnameChanged = true;
                }
            }

            // Update bio if provided and different (allow clearing bio with empty string)
            if (request.Bio != null)
            {
                var newBio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
                if (newBio != user.Bio)
                {
                    user.Bio = newBio;
                    changes.BioChanged = true;
                }
            }

            // Check if any changes were actually made
            if (!changes.FirstnameChanged && !changes.LastnameChanged && !changes.BioChanged)
            {
                _logger.LogDebug("No changes detected for user profile update: {UserId}", user.Id);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.NO_CHANGES_DETECTED,
                    "No changes were detected in the provided data",
                    new
                    {
                        UserId = user.Id,
                        SuggestedAction = "Provide different values to update your profile"
                    }
                ));
            }

            _logger.LogDebug("Profile changes detected: {Changes}", new
            {
                FirstnameChanged = changes.FirstnameChanged,
                LastnameChanged = changes.LastnameChanged,
                BioChanged = changes.BioChanged,
                UserId = user.Id
            });

            await _unitOfWork.UserRepository.UpdateAsync(user, cancellationToken);

            _logger.LogDebug("Saving changes to database");
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var updatedAt = DateTime.UtcNow;

            _logger.LogDebug("User profile updated successfully for user: {UserId}", user.Id);

            return Result.Ok(new UpdateUserCommandResponse
            {
                UserId = user.Id,
                Firstname = user.Firstname,
                Lastname = user.Lastname,
                Bio = user.Bio,
                UpdatedAt = updatedAt,
                ChangedFields = changes
            });
        }
    }
}
