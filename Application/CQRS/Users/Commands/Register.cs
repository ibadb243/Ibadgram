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

namespace Application.CQRS.Users.Commands.Register
{
    public class TokenResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }

    public class RegisterUserCommand : IRequest<TokenResponse>
    {
        public string? Fullname { get; set; }
        public string? Shortname { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
    }

    public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
    {
        public RegisterUserCommandValidator()
        {
            RuleFor(x => x.Fullname).NotEmpty().MaximumLength(256);
            RuleFor(x => x.Shortname).NotEmpty().MinimumLength(4).MaximumLength(64);
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(64);
        }
    }

    public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, TokenResponse>
    {
        private readonly IUserRepository _userRepository;
        private readonly IMentionRepository _mentionRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher _passwordHasher;

        public RegisterUserCommandHandler(
            IUserRepository userRepository,
            IMentionRepository mentionRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IChatRepository chatRepository,
            IUnitOfWork unitOfWork,
            ITokenService tokenService,
            IPasswordHasher passwordHasher)
        {
            _userRepository = userRepository;
            _mentionRepository = mentionRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _chatRepository = chatRepository;
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
        }

        public async Task<TokenResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
        {
            if (await _userRepository.GetByEmailAsync(request.Email, cancellationToken) != null)
                throw new Exception("Email already uses");

            var user = new User
            {
                Fullname = request.Fullname,
                Email = request.Email,
                PasswordHash = _passwordHasher.HashPassword(request.Password),
            };

            await _userRepository.AddAsync(user, cancellationToken);

            if (await _mentionRepository.GetByShortnameAsync(request.Shortname, cancellationToken) != null)
                throw new Exception("Shortname already taken");

            var chat = new PersonalChat
            {
                UserId = user.Id,
                User = user,
            };

            await _chatRepository.AddAsync(chat, cancellationToken);

            var mention = new UserMention
            {
                Shortname = request.Shortname,
                UserId = user.Id,
                User = user,
            };
            user.Mention = mention;

            await _mentionRepository.AddAsync(mention, cancellationToken);

            var at = _tokenService.GenerateAccessToken(user);

            var refresh_token = new RefreshToken
            {
                UserId = user.Id,
                AccessToken = at,
                Token = _tokenService.GenerateRefreshToken(at),
                ExpiresAtUtc = DateTime.UtcNow.AddDays(6),
                User = user
            };

            await _refreshTokenRepository.AddAsync(refresh_token, cancellationToken);
            await _unitOfWork.SaveAsync(cancellationToken);

            return new TokenResponse { AccessToken = at, RefreshToken = refresh_token.Token };
        }
    }
}
