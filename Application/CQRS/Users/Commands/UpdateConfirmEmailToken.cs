using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Common.Constants;
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
    public class UpdateConfirmEmailTokenCommand : IRequest<Result>
    {
        public string Email { get; set; }
    }

    public class UpdateConfirmEmailTokenCommandValidator : AbstractValidator<UpdateConfirmEmailTokenCommand>
    {
        private readonly IUserRepository _userRepository;

        public UpdateConfirmEmailTokenCommandValidator(IUserRepository userRepository)
        {
            _userRepository = userRepository;

            RuleFor(x => x.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithMessage("Email is required")
                .EmailAddress()
                    .WithMessage("Email address doesn't correct")
                .Matches(BuildEmailPattern())
                    .WithMessage("Allowed only Gmail, Yahoo, Yandex and Mail emails");
            //.MustAsync(BeExistEmail)
            //.WithMessage("Email not registered")
            //.MustAsync(BeNotAlreadyConfirmed)
            //.WithMessage("Email has already confirmed");
        }

        private string BuildEmailPattern()
        {
            var escapedDomains = EmailConstants.AllowedDomains.Select(d => d.Replace(".", @"\."));
            return $@"^.*@({string.Join("|", escapedDomains)})$";
        }

        private async Task<bool> BeExistEmail(string email, CancellationToken cancellationToken)
        {
            return await _userRepository.EmailExistsAsync(email, cancellationToken);
        }

        private async Task<bool> BeNotAlreadyConfirmed(string email, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            return user != null && !user.EmailConfirmed;
        }
    }

    public class UpdateConfirmEmailTokenCommandHandler : IRequestHandler<UpdateConfirmEmailTokenCommand, Result>
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

        public async Task<Result> Handle(UpdateConfirmEmailTokenCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting email confirmation token update process for user {Email}", request.Email);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Retrieving user by email {Email} from database", request.Email);
                var user = await _unitOfWork.UserRepository.GetByEmailAsync(request.Email, cancellationToken);

                if (user == null)
                {
                    _logger.LogWarning("Email confirmation token update failed - user with email {Email} not found", request.Email);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail($"User with email {request.Email} not found");
                }

                _logger.LogDebug("User {UserId} found with email confirmation status: {EmailConfirmed}", user.Id, user.EmailConfirmed);

                if (user.EmailConfirmed)
                {
                    _logger.LogWarning("Email confirmation token update failed - user {UserId} email already confirmed", user.Id);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User's email is confirmed");
                }

                _logger.LogDebug("Generating new email confirmation token for user {UserId}", user.Id);
                var emailToken = GenerateEmailConfirmationToken();

                user.EmailConfirmationToken = emailToken;
                user.EmailConfirmationTokenExpiry = DateTime.UtcNow.AddMinutes(5);
                _logger.LogDebug("Email confirmation token will expire at {TokenExpiry} for user {UserId}", user.EmailConfirmationTokenExpiry, user.Id);

                _logger.LogDebug("Updating user {UserId} with new email confirmation token", user.Id);
                await _unitOfWork.UserRepository.UpdateAsync(user, cancellationToken);

                _logger.LogDebug("Saving changes to database for user {UserId}", user.Id);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Committing transaction for user {UserId}", user.Id);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogDebug("Sending confirmation email");
                try
                {
                    await _emailSender.SendEmailAsync(
                        email: request.Email,
                        subject: "Code for confirm email",
                        htmlMessage: $@"
                            <h2>Welcome to Ibadgram!</h2>
                            <p>Code: <span>{emailToken}</span></p>
                        ");

                    _logger.LogInformation("Confirmation email sent successfully");
                }
                catch (Exception emailEx)
                {
                    _logger.LogError(emailEx, "Failed to send confirmation email - user created but email not sent");
                }

                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update email confirmation code failed");

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during update email confirmation code");
                }

                throw;
            }
        }

        private string GenerateEmailConfirmationToken() => RandomNumberGenerator.GetHexString(6);
    }
}
