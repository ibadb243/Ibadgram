using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Common.Constants;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Shortname { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; }
    }

    public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
    {
        public RegisterUserCommandValidator()
        {
            RuleFor(x => x.Firstname)
                .NotEmpty()
                .MinimumLength(UserConstants.FirstnameMinLength)
                .MaximumLength(UserConstants.FirstnameMaxLength);

            RuleFor(x => x.Lastname)
                .MaximumLength(UserConstants.LastnameLength);

            RuleFor(x => x.Shortname)
                .NotEmpty()
                .MinimumLength(4)
                .MaximumLength(64);

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress()
                .Matches(BuildEmailPattern())
                .WithMessage("Allowed only Gmail, Yahoo, Yandex and Mail emails");

            RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(64);

        }

        private string BuildEmailPattern()
        {
            var escapedDomains = EmailConstants.AllowedDomains.Select(d => d.Replace(".", @"\."));
            return $@"^.*@({string.Join("|", escapedDomains)})$";
        }
    }

    public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, TokenResponse>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher _passwordHasher;

        public RegisterUserCommandHandler(
            IUnitOfWork unitOfWork,
            ITokenService tokenService,
            IPasswordHasher passwordHasher)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
        }

        public async Task<TokenResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                if (await _unitOfWork.UserRepository.GetByEmailAsync(request.Email, cancellationToken) != null)
                    throw new Exception("Email already uses");

                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Firstname = request.Firstname,
                    Lastname = request.Lastname,
                    Email = request.Email,
                    PasswordHash = _passwordHasher.HashPassword(request.Password),
                };

                await _unitOfWork.UserRepository.AddAsync(user, cancellationToken);

                if (await _unitOfWork.MentionRepository.GetByShortnameAsync(request.Shortname, cancellationToken) != null)
                    throw new Exception("Shortname already taken");

                var chat = new Chat
                {
                    Id= Guid.NewGuid(),
                    Type = ChatType.Personal,
                };

                await _unitOfWork.ChatRepository.AddAsync(chat, cancellationToken);

                var member = new ChatMember
                {
                    ChatId = chat.Id,
                    Chat = chat,
                    UserId = user.Id,
                    User = user,
                };

                await _unitOfWork.ChatMemberRepository.AddAsync(member, cancellationToken);

                var mention = new UserMention
                {
                    Id = Guid.NewGuid(),
                    Shortname = request.Shortname,
                    UserId = user.Id,
                    User = user,
                };
                user.Mention = mention;

                await _unitOfWork.MentionRepository.AddAsync(mention, cancellationToken);

                var accessTokem = _tokenService.GenerateAccessToken(user);
                var refresToken = _tokenService.GenerateRefreshToken(user, accessTokem);

                await _unitOfWork.RefreshTokenRepository.AddAsync(refresToken, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new TokenResponse { AccessToken = accessTokem, RefreshToken = refresToken.Token };
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
