using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Entities;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Users.Commands.Login
{
    public class TokenResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }

    public class LoginUserCommand : IRequest<TokenResponse>
    {
        public string? Email { get; set; }
        public string? Password { get; set; }
    }

    public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
    {
        public LoginUserCommandValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(64);
        }
    }

    public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, TokenResponse>
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

        public async Task<TokenResponse> Handle(LoginUserCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _unitOfWork.UserRepository.GetByEmailAsync(request.Email, cancellationToken);
                if (user == null) throw new Exception("Invalid email or password!");

                if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
                    throw new Exception("Invalid email or password!");

                var at = _tokenService.GenerateAccessToken(user);

                var refresh_token = new RefreshToken
                {
                    UserId = user.Id,
                    AccessToken = at,
                    Token = _tokenService.GenerateRefreshToken(at),
                    ExpiresAtUtc = DateTime.UtcNow.AddDays(6),
                    User = user
                };

                await _unitOfWork.RefreshTokenRepository.AddAsync(refresh_token, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new TokenResponse { AccessToken = at, RefreshToken = refresh_token.Token };
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
