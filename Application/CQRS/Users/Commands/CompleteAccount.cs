using Application.Interfaces.Repositories;
using Domain.Common;
using Domain.Common.Constants;
using Domain.Entities;
using Domain.Errors;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Application.CQRS.Users.Commands.CompleteAccount
{
    public class CompleteAccountCommand : IRequest<Result<Guid>>
    {
        public Guid UserId { get; set; }
        public string Shortname { get; set; }
        public string? Bio { get; set; }
    }

    public class CompleteAccountCommandValidator : AbstractValidator<CompleteAccountCommand>
    {
        private readonly IMentionRepository _mentionRepository;

        public CompleteAccountCommandValidator(IMentionRepository mentionRepository)
        {
            _mentionRepository = mentionRepository;

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

            RuleFor(x => x.Bio)
                .Cascade(CascadeMode.Stop)
                .MaximumLength(UserConstants.BioLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_LONG)
                    .WithMessage($"Bio cannot exceed {UserConstants.BioLength} characters")
                .Must(BeValidBioContent)
                    .WithErrorCode(ErrorCodes.INVALID_CONTENT)
                    .WithMessage("Bio contains inappropriate content")
                .When(x => !string.IsNullOrEmpty(x.Bio));
        }

        private bool BeValidUsernameFormat(string shortname)
        {
            if (string.IsNullOrEmpty(shortname)) return false;

            // Не должен начинаться или заканчиваться спецсимволами
            return !Regex.IsMatch(shortname, @"^[._-]|[._-]$") &&
                   // Не должен содержать последовательные спецсимволы
                   !Regex.IsMatch(shortname, @"[._-]{2,}");
        }

        private bool BeValidBioContent(string bio)
        {
            if (string.IsNullOrEmpty(bio)) return true;

            var forbiddenPatterns = new[]
            {
                @"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", // XSS
                @"javascript:", // JavaScript URLs
                @"data:text\/html" // Data URLs
            };

            return !forbiddenPatterns.Any(pattern =>
                Regex.IsMatch(bio, pattern, RegexOptions.IgnoreCase));
        }
    }

    public class CompleteAccountCommandHandler : IRequestHandler<CompleteAccountCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CompleteAccountCommandHandler> _logger;

        public CompleteAccountCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<CompleteAccountCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CompleteAccountCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting account completion process for user: {UserId}", request.UserId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var userResult = await GetAndValidateUserAsync(request.UserId, cancellationToken);
                if (userResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return userResult.ToResult();
                }

                var user = userResult.Value;

                var shortnameResult = await ValidateShortnameAvailabilityAsync(request.Shortname, cancellationToken);
                if (shortnameResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return shortnameResult;
                }

                await CompleteUserAccountAsync(user, request, cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Account completion successful for user: {UserId} with username: {Username}",
                    user.Id, request.Shortname);

                return Result.Ok(user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account completion failed for user: {UserId}", request.UserId);

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during account completion");
                }

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to complete account setup due to system error"
                ));
            }
        }

        private async Task<Result<User>> GetAndValidateUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving user by ID: {UserId}", userId);

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Account completion failed - user not found: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_FOUND,
                    "User not found",
                    new { UserId = userId }
                ));
            }

            if (!user.EmailConfirmed)
            {
                _logger.LogWarning("Account completion failed - email not confirmed for user: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.EMAIL_NOT_CONFIRMED,
                    "Email address must be confirmed before completing account setup",
                    new
                    {
                        UserId = userId,
                        Email = user.Email,
                        SuggestedAction = "Please confirm your email address first"
                    }
                ));
            }

            if (user.IsVerified)
            {
                _logger.LogWarning("Account completion failed - account already completed for user: {UserId}", userId);

                var existingMention = await _unitOfWork.UserMentionRepository.GetByUserIdAsync(userId, cancellationToken);

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.ACCOUNT_ALREADY_COMPLETED,
                    "Account setup is already completed",
                    new
                    {
                        UserId = userId,
                        ExistingUsername = existingMention?.Shortname,
                        SuggestedAction = "You can now use the application"
                    }
                ));
            }

            _logger.LogDebug("User validation successful: {UserId}", userId);
            return Result.Ok(user);
        }

        private async Task<Result> ValidateShortnameAvailabilityAsync(string shortname, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Checking username availability: {Username}", shortname);

            var exists = await _unitOfWork.MentionRepository.ExistsByShortnameAsync(shortname, cancellationToken);

            if (exists)
            {
                _logger.LogWarning("Username already taken: {Username}", shortname);

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USERNAME_ALREADY_TAKEN,
                    "This username is already taken",
                    new
                    {
                        RequestedUsername = shortname,
                        SuggestedAction = "Choose a different username"
                    }
                ));
            }

            _logger.LogDebug("Username available: {Username}", shortname);
            return Result.Ok();
        }

        private async Task CompleteUserAccountAsync(User user, CompleteAccountCommand request, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Updating user account completion: {UserId}", user.Id);

            user.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
            user.IsVerified = true;

            await _unitOfWork.UserRepository.UpdateAsync(user, cancellationToken);

            var mention = new UserMention
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Shortname = request.Shortname.Trim().ToLowerInvariant(),
            };

            _logger.LogDebug("Creating username mention: {MentionId} for user: {UserId}", mention.Id, user.Id);
            await _unitOfWork.MentionRepository.AddAsync(mention, cancellationToken);

            _logger.LogDebug("Saving changes to database");
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
