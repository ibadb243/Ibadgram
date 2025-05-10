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

namespace Application.CQRS.Users.Commands.Refresh
{
    public class TokenResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }

    public class RefreshTokenCommand : IRequest<TokenResponse>
    {
        public string? RefreshToken { set; get; }
    }

    public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
    {
        public RefreshTokenCommandValidator()
        {
            RuleFor(x => x.RefreshToken).NotEmpty();
        }
    }

    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, TokenResponse>
    {
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;

        public RefreshTokenCommandHandler(
            IRefreshTokenRepository refreshTokenRepository,
            IUnitOfWork unitOfWork,
            ITokenService tokenService)
        {
            _refreshTokenRepository = refreshTokenRepository;
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
        }

        public async Task<TokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            var refresh_token = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);
            if (refresh_token == null)
                throw new Exception("Invalid refresh token");

            if (refresh_token.User == null)
                throw new Exception("User not found");

            if (DateTime.UtcNow > refresh_token.ExpiresAtUtc)
                throw new Exception("Refresh token expired");

            var at = _tokenService.GenerateAccessToken(refresh_token.User);

            refresh_token.AccessToken = at;
            refresh_token.Token = _tokenService.GenerateRefreshToken(at);
            refresh_token.ExpiresAtUtc = DateTime.UtcNow.AddDays(6);

            await _refreshTokenRepository.UpdateAsync(refresh_token, cancellationToken);

            await _unitOfWork.SaveAsync(cancellationToken);

            return new TokenResponse { AccessToken = at, RefreshToken = refresh_token.Token };
        }
    }
}
