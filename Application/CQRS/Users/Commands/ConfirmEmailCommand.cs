using Application.Interfaces.Repositories;
using Domain.Common.Constants;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Users.Commands.ConfirmEmail
{
    public class ConfirmEmailCommand : IRequest<Result>
    {
        public string Email { get; set; }
        public string Code { get; set; }
    }

    public class ConfirmEmailCommandValidator : AbstractValidator<ConfirmEmailCommand>
    {
        private readonly IUserRepository _userRepository;

        public ConfirmEmailCommandValidator(IUserRepository userRepository)
        {
            _userRepository = userRepository;

            RuleFor(x => x.Email)
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

            RuleFor(x => x.Code)
                .NotEmpty()
                    .WithMessage("Code is required");

            //RuleFor(x => x)
            //    .MustAsync(BeValidConfirmationCode)
            //    .WithMessage("Invalid or expired confirmed code");
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

        private async Task<bool> BeValidConfirmationCode(ConfirmEmailCommand command, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(command.Email, cancellationToken);
            if (user == null) return false;

            return user.EmailConfirmationToken == command.Code &&
                   user.EmailConfirmationTokenExpiry > DateTime.UtcNow;
        }
    }

    public class ConfirmEmailCommandHandler : IRequestHandler<ConfirmEmailCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ConfirmEmailCommandHandler> _logger;

        public ConfirmEmailCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<ConfirmEmailCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting email confirmation proccess");

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Retrieving user by email {Email} from database", request.Email);
                var user = await _unitOfWork.UserRepository.GetByEmailAsync(request.Email, cancellationToken);

                if (user == null)
                {
                    _logger.LogWarning("Email confirmation failed - user not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail($"User with email {request.Email} not found");
                }

                _logger.LogDebug("User found: {UserId}, checking confirmation status", user.Id);

                if (user.EmailConfirmed)
                {
                    _logger.LogWarning("Email confirmation failed - email {Email} already confirmed", request.Email);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Email has already been confirmed");
                }

                if (string.IsNullOrEmpty(user.EmailConfirmationToken))
                {
                    _logger.LogCritical("Email confirmation failed - no confirmation token found: {UserId}", user.Id);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("No confirmation code found for this email");
                }

                if (user.EmailConfirmationToken != request.Code)
                {
                    _logger.LogWarning("Email confirmation failed - invalid confirmation code: {Code}", request.Code);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Invalid confirmation code");
                }

                if (user.EmailConfirmationTokenExpiry.HasValue && user.EmailConfirmationTokenExpiry.Value < DateTime.UtcNow)
                {
                    _logger.LogWarning("Email confirmation failed - confirmation code expired: {Code} {ExpiredAt}",
                        user.EmailConfirmationToken, user.EmailConfirmationTokenExpiry.Value);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Confirmation code has expired");
                }

                _logger.LogDebug("Confirmation code validated successfully, updating user: {UserId}", user.Id);

                user.EmailConfirmed = true;
                user.EmailConfirmationToken = null;
                user.EmailConfirmationTokenExpiry = null;

                _logger.LogDebug("Saving user changes to database");
                await _unitOfWork.UserRepository.UpdateAsync(user, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Committing transaction");
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Email confirmation completed successfully for user {UserId}", user.Id);

                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email confirmation failed");

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during email confirmation");
                }

                throw;
            }
        }
    }
}
