using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Entities;
using FluentResults;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Users.Commands.Refresh
{
    public class RefreshTokenResponse
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }

    public class RefreshTokenCommand : IRequest<Result<RefreshTokenResponse>>
    {
        public string RefreshToken { set; get; }
    }

    public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
    {
        private readonly IRefreshTokenRepository _refreshTokenRepository;

        public RefreshTokenCommandValidator(IRefreshTokenRepository refreshTokenRepository)
        {
            _refreshTokenRepository = refreshTokenRepository;

            RuleFor(x => x.RefreshToken)
                .NotEmpty()
                .MustAsync(BeExistToken)
                .WithMessage("Token not exists")
                .MustAsync(BeValid)
                .WithMessage("Token not valid");
        }

        private async Task<bool> BeExistToken(string refreshToken, CancellationToken cancellationToken)
        {
            return await _refreshTokenRepository.TokenExistsAsync(refreshToken, cancellationToken);
        }

        private async Task<bool> BeValid(string refreshToken, CancellationToken cancellationToken)
        {
            var token = await _refreshTokenRepository.GetByTokenAsync(refreshToken, cancellationToken);
            return token != null && !token.IsRevoked && token.ExpiresAtUtc > DateTime.UtcNow;
        }
    }

    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;

        public RefreshTokenCommandHandler(
            IUnitOfWork unitOfWork,
            ITokenService tokenService)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
        }

        public async Task<Result<RefreshTokenResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var refreshToken = await _unitOfWork.RefreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);
                if (refreshToken == null) return Result.Fail("Refresh Token not found");

                var accessToken = _tokenService.GenerateAccessToken(refreshToken.User);
                refreshToken = _tokenService.UpdateRefreshToken(refreshToken, accessToken);

                await _unitOfWork.RefreshTokenRepository.UpdateAsync(refreshToken, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new RefreshTokenResponse 
                {
                    AccessToken = accessToken, 
                    RefreshToken = refreshToken.Token 
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
