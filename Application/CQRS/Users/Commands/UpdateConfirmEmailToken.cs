using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
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
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Users.Commands.UpdateConfirmEmailToken
{
    public class UpdateConfirmEmailTokenCommand : IRequest<Result<UpdateConfirmEmailTokenResponse>>
    {
        public Guid UserId { get; set; }
    }

    public class UpdateConfirmEmailTokenResponse
    {
        public string Email { get; set; } = string.Empty;
        public DateTime TokenExpiresAt { get; set; }
        public string Message { get; set; }
    }

    public class UpdateConfirmEmailTokenCommandValidator : AbstractValidator<UpdateConfirmEmailTokenCommand>
    {
        private readonly IUserRepository _userRepository;

        public UpdateConfirmEmailTokenCommandValidator(IUserRepository userRepository)
        {
            _userRepository = userRepository;

            RuleFor(x => x.UserId)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("UserId is required");
        }
    }

    public class UpdateConfirmEmailTokenCommandHandler : IRequestHandler<UpdateConfirmEmailTokenCommand, Result<UpdateConfirmEmailTokenResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<UpdateConfirmEmailTokenCommandHandler> _logger;

        public UpdateConfirmEmailTokenCommandHandler(
            IUnitOfWork unitOfWork,
            IEmailSender emailSender,
            ILogger<UpdateConfirmEmailTokenCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _emailSender = emailSender;
            _logger = logger;
        }

        public async Task<Result<UpdateConfirmEmailTokenResponse>> Handle(UpdateConfirmEmailTokenCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting email confirmation token update for user {UserId}", request.UserId);

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

                var tokenExpiryTime = await UpdateUserTokenAsync(user, cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Email confirmation token updated successfully for user {UserId}", user.Id);

                _ = Task.Run(async () => await SendConfirmationEmailAsync(user.Email, user.EmailConfirmationToken), cancellationToken);

                return Result.Ok(new UpdateConfirmEmailTokenResponse
                {
                    Email = user.Email,
                    TokenExpiresAt = tokenExpiryTime,
                    Message = "New confirmation code has been sent to your email address"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update email confirmation token for user {UserId}", request.UserId);

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction");
                }

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to update email confirmation token account due to system error"
                ));
            }
        }

        private async Task<Result<User>> GetAndValidateUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving user by ID: {UserId}", userId);

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Email confirmation token update failed - user not found: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_FOUND,
                    "User not found",
                    new { UserId = userId }
                ));
            }

            if (user.EmailConfirmed)
            {
                _logger.LogWarning("Email confirmation token update failed - email already confirmed for user: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.EMAIL_ALREADY_CONFIRMED,
                    "Email has already been confirmed",
                    new
                    {
                        UserId = userId,
                        Email = user.Email,
                        SuggestedAction = "Email is already confirmed. You can proceed with your account."
                    }
                ));
            }

            _logger.LogDebug("User validation successful: {UserId}", userId);
            return Result.Ok(user);
        }

        private async Task<DateTime> UpdateUserTokenAsync(User user, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Generating new email confirmation token for user {UserId}", user.Id);

            var emailToken = GenerateEmailConfirmationToken();
            var tokenExpiry = DateTime.UtcNow + EmailConstants.EmailConfirmationTokenExpiry;

            user.EmailConfirmationToken = emailToken;
            user.EmailConfirmationTokenExpiry = tokenExpiry;

            _logger.LogDebug("Token will expire at {TokenExpiry} for user {UserId}", tokenExpiry, user.Id);

            await _unitOfWork.UserRepository.UpdateAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return tokenExpiry;
        }

        private async Task SendConfirmationEmailAsync(string email, string token)
        {
            try
            {
                await _emailSender.SendEmailAsync(
                    email: email,
                    subject: "Confirm your email address",
                    htmlMessage: $@"
                    <h2>Welcome to Ibadgram!</h2>
                    <p>Your confirmation code: <strong>{token}</strong></p>
                    <p>This code will expire in 5 minutes.</p>
                ");

                _logger.LogInformation("Confirmation email sent successfully to: {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email to: {Email}", email);
            }
        }

        private string GenerateEmailConfirmationToken() => RandomNumberGenerator.GetHexString(EmailConstants.EmailConfirmationTokenLength);
    }
}
