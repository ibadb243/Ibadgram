using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Common;
using Domain.Entities;
using Domain.Errors;
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
    public class RefreshTokenCommand : IRequest<Result<RefreshTokenResponse>>
    {
        public string RefreshToken { set; get; } = string.Empty;
    }

    public class RefreshTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
    {
        public RefreshTokenCommandValidator()
        {
            RuleFor(x => x.RefreshToken)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("Refresh token is required");
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
                var tokenResult = await GetAndValidateTokenAsync(request.RefreshToken, cancellationToken);
                if (tokenResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return tokenResult.ToResult();
                }

                var refreshToken = tokenResult.Value;

                var userResult = await GetAndValidateUserAsync(refreshToken.UserId, cancellationToken);
                if (userResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return userResult.ToResult();
                }

                var user = userResult.Value;

                var tokens = await UpdateTokensAsync(user, refreshToken, cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Refresh token completed successfully for user: {UserId}", user.Id);

                return Result.Ok(new RefreshTokenResponse
                {
                    AccessToken = tokens.AccessToken,
                    RefreshToken = tokens.RefreshToken
                });
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

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to refresh token due to system error"
                ));
            }
        }

        private async Task<Result<RefreshToken>> GetAndValidateTokenAsync(string tokenValue, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving refresh token from database");

            var refreshToken = await _unitOfWork.RefreshTokenRepository.GetByTokenAsync(tokenValue, cancellationToken);

            if (refreshToken == null)
            {
                _logger.LogWarning("Refresh token failed - token not found");
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.REFRESH_TOKEN_NOT_FOUND,
                    "Refresh token not found"
                ));
            }

            if (refreshToken.IsRevoked)
            {
                _logger.LogWarning("Refresh token failed - token is revoked: {TokenId}", refreshToken.Id);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.REFRESH_TOKEN_REVOKED,
                    "Refresh token has been revoked"
                ));
            }

            if (refreshToken.ExpiresAtUtc < DateTime.UtcNow)
            {
                _logger.LogWarning("Refresh token failed - token is expired: {TokenId}", refreshToken.Id);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.REFRESH_TOKEN_EXPIRED,
                    "Refresh token has expired"
                ));
            }

            _logger.LogDebug("Refresh token validation successful: {TokenId}", refreshToken.Id);
            return Result.Ok(refreshToken);
        }

        private async Task<Result<User>> GetAndValidateUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving user by ID: {UserId}", userId);

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.LogCritical("Refresh token failed - user not found: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_FOUND,
                    "Access forbidden"
                ));
            }

            if (user.IsDeleted)
            {
                _logger.LogCritical("Refresh token failed - user is deleted: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_DELETED,
                    "User account has been deleted"
                ));
            }

            _logger.LogDebug("User validation successful: {UserId}", userId);
            return Result.Ok(user);
        }

        private async Task<(string AccessToken, string RefreshToken)> UpdateTokensAsync(User user, RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Generating new access token and updating refresh token for user: {UserId}", user.Id);

            var accessToken = _tokenService.GenerateAccessToken(user);
            var updatedRefreshToken = _tokenService.UpdateRefreshToken(refreshToken, accessToken);

            await _unitOfWork.RefreshTokenRepository.UpdateAsync(updatedRefreshToken, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return (accessToken, updatedRefreshToken.Token);
        }
    }
}
