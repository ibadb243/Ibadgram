using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Entities;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
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
                    .WithMessage("RefreshToken is required");
                //.MustAsync(BeExistToken)
                //.WithMessage("Token not exists")
                //.MustAsync(BeValid)
                //.WithMessage("Token not valid");
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
        private readonly ILogger<RefreshTokenCommandHandler> _logger;

        public RefreshTokenCommandHandler(
            IUnitOfWork unitOfWork,
            ITokenService tokenService,
            ILogger<RefreshTokenCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
            _logger = logger;
        }

        public async Task<Result<RefreshTokenResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting refresh token process");

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Retrieving refresh token {token} from database", request.RefreshToken);
                var refreshToken = await _unitOfWork.RefreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);

                if (refreshToken == null)
                {
                    _logger.LogWarning("Refresh token failed - token not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Refresh Token not found");
                }

                _logger.LogDebug("Retrieving user by id {UserId} from database", refreshToken.UserId);
                var user = await _unitOfWork.UserRepository.GetByIdAsync(refreshToken.UserId, cancellationToken);

                if (user == null)
                {
                    _logger.LogCritical("Refresh token failed - user not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Access forbidden");
                }

                if (user.IsDeleted)
                {
                    _logger.LogCritical("Refresh token failed - user is deleted");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User was deleted");
                }

                if (refreshToken.IsRevoked)
                {
                    _logger.LogWarning("Refresh token failed - token is revoked");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Refresh Token was revoked");
                }

                if (refreshToken.ExpiresAtUtc < DateTime.UtcNow)
                {
                    _logger.LogWarning("Refresh token failed - token is expired");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Refresh Token is expired");
                }

                _logger.LogDebug("Refresh token validated successfully");

                _logger.LogDebug("Generating Access Token and updating Refresh Token");
                var accessToken = _tokenService.GenerateAccessToken(refreshToken.User);
                refreshToken = _tokenService.UpdateRefreshToken(refreshToken, accessToken);

                _logger.LogDebug("Saving user changes to database");
                await _unitOfWork.RefreshTokenRepository.UpdateAsync(refreshToken, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Committing transaction");
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Refresh token completed successfully for user {UserId}", );

                return new RefreshTokenResponse 
                {
                    AccessToken = accessToken, 
                    RefreshToken = refreshToken.Token 
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh token failed");

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during refresh token");
                }

                throw;
            }
        }
    }
}
