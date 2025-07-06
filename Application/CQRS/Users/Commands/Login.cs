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
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Users.Commands.Login
{
    public class LoginUserCommandResponse
    {
        public Guid UserId { get; set; }
        public string Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Bio { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }

    public class LoginUserCommand : IRequest<Result<LoginUserCommandResponse>>
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
    {
        private readonly IUserRepository _userRepository;

        public LoginUserCommandValidator(IUserRepository userRepository)
        {
            _userRepository = userRepository;

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .Matches(BuildEmailPattern())
                .WithMessage("Allowed only Gmail, Yahoo, Yandex and Mail emails")
                .MustAsync(BeExistEmail)
                .WithMessage("Email not registered")
                .MustAsync(BeVerified)
                .WithMessage("Email do not pass registration");

            RuleFor(x => x.Password)
                .NotEmpty()
                .MinimumLength(8)
                .MaximumLength(64);
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

        private async Task<bool> BeVerified(string email, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
            return user != null && user.IsVerified;
        }
    }

    public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, Result<LoginUserCommandResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher _passwordHasher;

        public LoginUserCommandHandler(
            IUnitOfWork unitOfWork,
            ITokenService tokenService,
            IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
        }

        public async Task<Result<LoginUserCommandResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _unitOfWork.UserRepository.GetByEmailAsync(request.Email, cancellationToken);
                if (user == null) return Result.Fail($"User with email {request.Email} not found");

                if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordSalt, user.PasswordHash))
                    throw new Exception("Invalid email or password!");

                var accessToken = _tokenService.GenerateAccessToken(user);
                var refreshToken = _tokenService.GenerateRefreshToken(user, accessToken);

                await _unitOfWork.RefreshTokenRepository.AddAsync(refreshToken, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new LoginUserCommandResponse
                {
                    UserId = user.Id,
                    Firstname = user.Firstname,
                    Lastname = user.Lastname,
                    Bio = user.Bio,
                    AccessToken = accessToken,
                    RefreshToken = refreshToken.Token,
                };
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
