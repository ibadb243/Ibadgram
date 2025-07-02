using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Common.Constants;
using Domain.Entities;
using FluentResults;
using FluentValidation;
using MediatR;
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
                .NotEmpty()
                .MinimumLength(UserConstants.FirstnameMinLength)
                .MaximumLength(UserConstants.FirstnameMaxLength);

            RuleFor(x => x.Lastname)
                .MaximumLength(UserConstants.LastnameLength);

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .Matches(BuildEmailPattern())
                .WithMessage("Allowed only Gmail, Yahoo, Yandex and Mail emails")
                .MustAsync(BeUniqueEmail)
                .WithMessage("Email has already been registered with an account");

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(UserConstants.PasswordMinLength)
                .MaximumLength(UserConstants.PasswordMaxLength);
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

        public CreateAccountCommandHandler(
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            IEmailSender emailSender)
        {
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _emailSender = emailSender;
        }

        public async Task<Result<Guid>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
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

                await _emailSender.SendEmailAsync(
                    email: request.Email,
                    subject: "Code for confirm email",
                    htmlMessage: $@"
                        <h2>Welcome to Ibadgram!</h2>
                        <p>Code: <span>{emailToken}</span></p>
                    ");

                await _unitOfWork.UserRepository.AddAsync(user, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return user.Id;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }

        private string GenerateEmailConfirmationToken() => RandomNumberGenerator.GetHexString(6);
    }
}
