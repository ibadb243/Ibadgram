using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Common.Constants;
using FluentResults;
using FluentValidation;
using MediatR;
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
                .NotEmpty()
                .EmailAddress()
                .Matches(BuildEmailPattern())
                .WithMessage("Allowed only Gmail, Yahoo, Yandex and Mail emails")
                .MustAsync(BeExistEmail)
                .WithMessage("Email not registered")
                .MustAsync(BeNotAlreadyConfirmed)
                .WithMessage("Email has already confirmed");
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

        public UpdateConfirmEmailTokenCommandHandler(
            IUnitOfWork unitOfWork,
            IEmailSender emailSender)
        {
            _unitOfWork = unitOfWork;
            _emailSender = emailSender;
        }

        public async Task<Result> Handle(UpdateConfirmEmailTokenCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _unitOfWork.UserRepository.GetByEmailAsync(request.Email, cancellationToken);
                if (user == null) return Result.Fail($"USer with email {request.Email} not found");

                var emailToken = GenerateEmailConfirmationToken();

                user.EmailConfirmationToken = emailToken;
                user.EmailConfirmationTokenExpiry = DateTime.UtcNow.AddMinutes(5);

                await _unitOfWork.UserRepository.UpdateAsync(user, cancellationToken);

                await _emailSender.SendEmailAsync(
                    email: request.Email,
                    subject: "Code for confirm email",
                    htmlMessage: $@"
                        <h2>Welcome to Ibadgram!</h2>
                        <p>Code: <span>{emailToken}</span></p>
                    ");

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return Result.Ok();
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
