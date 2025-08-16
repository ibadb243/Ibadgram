using Application.CQRS.Users.Queries.Get;
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

namespace Application.CQRS.Users.Commands.CreateAccount
{
    public class CreateAccountCommand : IRequest<Result<Guid>>
    {
        public string Firstname { get; set; }
        public string? Lastname { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
    {
        private readonly IUserRepository _userRepository;

        public CreateAccountCommandValidator(IUserRepository userRepository)
        {
            _userRepository = userRepository;

            RuleFor(x => x.Firstname)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("Firstname is required")
                .MinimumLength(UserConstants.FirstnameMinLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_SHORT)
                    .WithMessage($"Firstname's length should have minimum {UserConstants.FirstnameMinLength} characters")
                .MaximumLength(UserConstants.FirstnameMaxLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_LONG)
                    .WithMessage($"Firstname's length cann't have characters greater than {UserConstants.FirstnameMaxLength}");

            RuleFor(x => x.Lastname)
                .Cascade(CascadeMode.Stop)
                .MaximumLength(UserConstants.LastnameLength)
                    .WithErrorCode (ErrorCodes.FIELD_TOO_LONG)
                    .WithMessage($"Lastname's length cann't have characters greater than {UserConstants.LastnameLength}");

            RuleFor(x => x.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("Email is required")
                .EmailAddress()
                    .WithErrorCode(ErrorCodes.INVALID_FORMAT)
                    .WithMessage("Email address doesn't correct")
                .Matches(BuildEmailPattern())
                    .WithErrorCode(ErrorCodes.UNSUPPORTED_EMAIL_DOMAIN)
                    .WithMessage("Allowed only Gmail, Yahoo, Yandex and Mail emails");

            RuleFor(x => x.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("Password is required")
                .MinimumLength(UserConstants.PasswordMinLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_SHORT)
                    .WithMessage($"Password's length should have minimum {UserConstants.PasswordMinLength} characters")
                .MaximumLength(UserConstants.PasswordMaxLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_LONG)
                    .WithMessage($"Password's length cann't have characters greater than {UserConstants.PasswordMaxLength}");
        }

        private string BuildEmailPattern()
        {
            var escapedDomains = EmailConstants.AllowedDomains.Select(d => d.Replace(".", @"\."));
            return $@"^.*@({string.Join("|", escapedDomains)})$";
        }
    }

    public class CreateAccountCommandHandler : IRequestHandler<CreateAccountCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<CreateAccountCommandHandler> _logger;

        public CreateAccountCommandHandler(
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IEmailSender emailSender,
            ILogger<CreateAccountCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _emailSender = emailSender;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting account creation process for email: {Email}", request.Email);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var existingUserResult = await CheckExistingUser(request.Email, cancellationToken);
                if (existingUserResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return existingUserResult;
                }

                _logger.LogDebug("Generating password salt and email confirmation token");
                var salt = RandomNumberGenerator.GetHexString(64);
                var emailToken = GenerateEmailConfirmationToken();

                var user = await CreateUser(request);

                await _unitOfWork.UserRepository.AddAsync(user, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("User created successfully: {UserId}", user.Id);

                _ = Task.Run(async () => await SendConfirmationEmailAsync(request.Email, user.EmailConfirmationToken), cancellationToken);

                return Result.Ok(user.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account creation failed for email: {Email}", request.Email);

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction");
                }

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to create account due to system error"
                ));
            }
        }

        private async Task<Result<Guid>> CheckExistingUser(string email, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Checking existing user for email: {Email}", email);

            var existingUser = await _unitOfWork.UserRepository.GetByEmailAsync(email, cancellationToken);

            if (existingUser == null)
                return Result.Ok();

            _logger.LogWarning("Account creation failed - email already exists: {EmailStatus}", new
            {
                IsVerified = existingUser.IsVerified,
                EmailConfirmed = existingUser.EmailConfirmed,
                UserId = existingUser.Id
            });

            if (existingUser.IsVerified)
            {
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_ALREADY_VERIFIED,
                    "An account with this email address already exists and is verified",
                    new
                    {
                        Email = email,
                        SuggestedAction = "Try signing in or use password recovery",
                    }
                ));
            }

            if (existingUser.EmailConfirmed)
            {
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.EMAIL_ALREADY_CONFIRMED,
                    "Email address is already confirmed but account setup is incomplete",
                    new
                    {
                        Email = email,
                        SuggestedAction = "Complete your account setup",
                        UserId = existingUser.Id
                    }
                ));
            }

            return Result.Fail(new BusinessLogicError(
                ErrorCodes.EMAIL_AWAITING_CONFIRMATION,
                "Email address is already registered and awaiting confirmation",
                new
                {
                    Email = email,
                    SuggestedAction = "Check your email for confirmation code or request a new one",
                    TokenExpiry = existingUser.EmailConfirmationTokenExpiry,
                    CanResend = DateTime.UtcNow > existingUser.EmailConfirmationTokenExpiry?.AddMinutes(-2)
                }
            ));
        }

        private async Task<User> CreateUser(CreateAccountCommand request)
        {
            _logger.LogDebug("Creating new user entity");

            var salt = RandomNumberGenerator.GetHexString(64);
            var emailToken = GenerateEmailConfirmationToken();

            return new User
            {
                Id = Guid.NewGuid(),
                Firstname = request.Firstname,
                Lastname = request.Lastname,
                Email = request.Email,
                PasswordSalt = salt,
                PasswordHash = _passwordHasher.HashPassword(request.Password, salt),
                EmailConfirmed = false,
                EmailConfirmationToken = emailToken,
                EmailConfirmationTokenExpiry = DateTime.UtcNow.AddMinutes(5),
            };
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

        private string GenerateEmailConfirmationToken() => RandomNumberGenerator.GetHexString(6);
    }
}
