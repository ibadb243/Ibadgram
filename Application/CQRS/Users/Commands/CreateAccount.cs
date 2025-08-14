using Application.CQRS.Users.Queries.Get;
using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Common.Constants;
using Domain.Entities;
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
                    .WithMessage("Firstname is required")
                .MinimumLength(UserConstants.FirstnameMinLength)
                    .WithMessage($"Firstname's length should have minimum {UserConstants.FirstnameMinLength} characters")
                .MaximumLength(UserConstants.FirstnameMaxLength)
                    .WithMessage($"Firstname's length cann't have characters greater than {UserConstants.FirstnameMaxLength}");

            RuleFor(x => x.Lastname)
                .Cascade(CascadeMode.Stop)
                .MaximumLength(UserConstants.LastnameLength)
                    .WithMessage($"Lastname's length cann't have characters greater than {UserConstants.LastnameLength}");

            RuleFor(x => x.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithMessage("Email is required")
                .EmailAddress()
                    .WithMessage("Email address doesn't correct")
                .Matches(BuildEmailPattern())
                    .WithMessage("Allowed only Gmail, Yahoo, Yandex and Mail emails");
                //.MustAsync(BeUniqueEmail)
                //.WithMessage("Email has already been registered with an account");

            RuleFor(x => x.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithMessage("Password is required")
                .MinimumLength(UserConstants.PasswordMinLength)
                    .WithMessage($"Password's length should have minimum {UserConstants.PasswordMinLength} characters")
                .MaximumLength(UserConstants.PasswordMaxLength)
                    .WithMessage($"Password's length cann't have characters greater than {UserConstants.PasswordMaxLength}");
        }

        private string BuildEmailPattern()
        {
            var escapedDomains = EmailConstants.AllowedDomains.Select(d => d.Replace(".", @"\."));
            return $@"^.*@({string.Join("|", escapedDomains)})$";
        }

        private async Task<bool> BeUniqueEmail(string email, CancellationToken cancellationToken)
        {
            return !await _userRepository.EmailExistsAsync(email, cancellationToken);
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
            _logger.LogInformation("Starting account creation proccess");

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Checking if user with email {Email} already exists", request.Email);
                var userByEmail = await _unitOfWork.UserRepository.GetByEmailAsync(request.Email, cancellationToken);
                if (userByEmail != null)
                {
                    _logger.LogWarning("Account creation failed - email alredy exists: {EmailStatus}",
                        new
                        {
                            IsVerified = userByEmail.IsVerified,
                            EmailConfirmed = userByEmail.EmailConfirmed,
                            UserId = userByEmail.Id,
                        });

                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);

                    if (userByEmail.IsVerified) 
                        return Result.Fail("Email has already been used");
                    else if (userByEmail.EmailConfirmed) 
                        return Result.Fail("Email has already been confirmed");
                    else 
                        return Result.Fail("Email has awated confirmation");
                }

                _logger.LogDebug("Generating password salt and email confirmation token");
                var salt = RandomNumberGenerator.GetHexString(64);
                var emailToken = GenerateEmailConfirmationToken();

                var user = new User
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

                _logger.LogDebug("Adding user to database: {UserId}", user.Id);
                await _unitOfWork.UserRepository.AddAsync(user, cancellationToken);

                _logger.LogDebug("Saving changes to database");
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Committing transaction");
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("User created successfully in database: {UserId}", user.Id);

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

                return user.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account creation failed");

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during create account");
                }

                throw;
            }
        }

        private string GenerateEmailConfirmationToken() => RandomNumberGenerator.GetHexString(6);
    }
}
