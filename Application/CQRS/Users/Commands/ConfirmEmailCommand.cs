using Application.Interfaces.Repositories;
using Domain.Common.Constants;
using FluentResults;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
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
                .EmailAddress()
                .Matches(BuildEmailPattern())
                .WithMessage("Allowed only Gmail, Yahoo, Yandex and Mail emails")
                .MustAsync(BeExistEmail)
                .WithMessage("Email not registered")
                .MustAsync(BeNotAlreadyConfirmed)
                .WithMessage("Email has already confirmed");

            RuleFor(x => x.Code)
                .NotEmpty();

            RuleFor(x => x)
                .MustAsync(BeValidConfirmationCode)
                .WithMessage("Invalid or expired confirmed code");
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

        public ConfirmEmailCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(ConfirmEmailCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _unitOfWork.UserRepository.GetByEmailAsync(request.Email, cancellationToken);
                if (user == null) return Result.Fail($"User with email {request.Email} not found");

                user.EmailConfirmed = true;
                user.EmailConfirmationToken = null;
                user.EmailConfirmationTokenExpiry = null;

                await _unitOfWork.UserRepository.UpdateAsync(user, cancellationToken);

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
    }
}
