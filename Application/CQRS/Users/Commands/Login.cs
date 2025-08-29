using Application.Interfaces.Services;
using Domain.Common;
using Domain.Common.Constants;
using Domain.Entities;
using Domain.Errors;
using Domain.Repositories;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Users.Commands.Login
{
    public class LoginUserCommand : IRequest<Result<LoginUserCommandResponse>>
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public class LoginUserCommandResponse
    {
        public Guid UserId { get; set; }
        public string Firstname { get; set; } = string.Empty;
        public string? Lastname { get; set; }
        public string? Bio { get; set; }
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class LoginUserCommandValidator : AbstractValidator<LoginUserCommand>
    {
        public LoginUserCommandValidator()
        {
            RuleFor(x => x.Email)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("Email is required")
                .EmailAddress()
                    .WithErrorCode(ErrorCodes.INVALID_FORMAT)
                    .WithMessage("Email address format is invalid")
                .Matches(BuildEmailPattern())
                    .WithErrorCode(ErrorCodes.UNSUPPORTED_EMAIL_DOMAIN)
                    .WithMessage("Only Gmail, Yahoo, Yandex and Mail.ru emails are allowed");

            RuleFor(x => x.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("Password is required")
                .MinimumLength(UserConstants.PasswordMinLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_SHORT)
                    .WithMessage($"Password must be at least {UserConstants.PasswordMinLength} characters long")
                .MaximumLength(UserConstants.PasswordMaxLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_LONG)
                    .WithMessage($"Password cannot exceed {UserConstants.PasswordMaxLength} characters");
        }

        private string BuildEmailPattern()
        {
            var escapedDomains = EmailConstants.AllowedDomains.Select(d => d.Replace(".", @"\."));
            return $@"^.*@({string.Join("|", escapedDomains)})$";
        }
    }

    public class LoginUserCommandHandler : IRequestHandler<LoginUserCommand, Result<LoginUserCommandResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ILogger<LoginUserCommandHandler> _logger;

        public LoginUserCommandHandler(
            IUnitOfWork unitOfWork,
            ITokenService tokenService,
            IPasswordHasher passwordHasher,
            ILogger<LoginUserCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        public async Task<Result<LoginUserCommandResponse>> Handle(LoginUserCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting login process for email: {Email}", request.Email);

            await _unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);

            try
            {
                var userResult = await GetAndValidateUserAsync(request.Email, cancellationToken);
                if (userResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return userResult.ToResult();
                }

                var user = userResult.Value;

                var passwordValidationResult = ValidatePassword(user, request.Password);
                if (passwordValidationResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return passwordValidationResult;
                }

                var tokens = await GenerateTokensAsync(user, cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("User login successful for user: {UserId}", user.Id);

                return Result.Ok(new LoginUserCommandResponse
                {
                    UserId = user.Id,
                    Firstname = user.Firstname,
                    Lastname = user.Lastname,
                    Bio = user.Bio,
                    AccessToken = tokens.AccessToken,
                    RefreshToken = tokens.RefreshToken,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed for email: {Email}", request.Email);

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during login");
                }

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to process login due to system error"
                ));
            }
        }

        private async Task<Result<User>> GetAndValidateUserAsync(string email, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving user by email: {Email}", email);

            var user = await _unitOfWork.UserRepository.GetByEmailAsync(email, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Login failed - user not found for email: {Email}", email);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.INVALID_CREDENTIALS,
                    "Invalid email or password"
                ));
            }

            if (!user.IsVerified)
            {
                _logger.LogWarning("Login failed - user not verified: {UserId}", user.Id);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_VERIFIED,
                    "User account is not verified",
                    new
                    {
                        UserId = user.Id,
                        SuggestedAction = "Complete your account setup"
                    }
                ));
            }

            if (user.IsDeleted)
            {
                _logger.LogWarning("Login failed - user is deleted: {UserId}", user.Id);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_DELETED,
                    "User account has been deleted"
                ));
            }

            _logger.LogDebug("User validation successful: {UserId}", user.Id);
            return Result.Ok(user);
        }

        private Result ValidatePassword(User user, string password)
        {
            _logger.LogDebug("Validating password for user: {UserId}", user.Id);

            if (!_passwordHasher.VerifyPassword(password, user.PasswordSalt, user.PasswordHash))
            {
                _logger.LogWarning("Login failed - invalid password for user: {UserId}", user.Id);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.INVALID_CREDENTIALS,
                    "Invalid email or password"
                ));
            }

            _logger.LogDebug("Password validation successful for user: {UserId}", user.Id);
            return Result.Ok();
        }

        private async Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(User user, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Generating tokens for user: {UserId}", user.Id);

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken(user, accessToken);

            _logger.LogDebug("Adding refresh token to database: {RefreshTokenId}", refreshToken.Id);
            await _unitOfWork.RefreshTokenRepository.AddAsync(refreshToken, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return (accessToken, refreshToken.Token);
        }
    }
}
