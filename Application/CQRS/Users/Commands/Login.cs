using Application.Interfaces.Repositories;
using Application.Interfaces.Services;
using Domain.Common.Constants;
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
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithMessage("Email is required")
                .EmailAddress()
                    .WithMessage("Email address doesn't correct")
                .Matches(BuildEmailPattern())
                    .WithMessage("Allowed only Gmail, Yahoo, Yandex and Mail emails");
                //.MustAsync(BeExistEmail)
                //.WithMessage("Email not registered")
                //.MustAsync(BeVerified)
                //.WithMessage("Email do not pass registration");

            RuleFor(x => x.Password)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithMessage("Password is required")
                .MinimumLength(UserConstants.PasswordMinLength)
                    .WithMessage($"Password's length should have minimum {UserConstants.PasswordMinLength} characters")
                .MaximumLength(UserConstants.PasswordMaxLength)
                    .WithMessage($"Password's length cann't have characters greater than {UserConstants.PasswordMaxLength}");
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
            _logger.LogInformation("Starting login proccess");

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Retrieving user by email {Email} from database", request.Email);
                var user = await _unitOfWork.UserRepository.GetByEmailAsync(request.Email, cancellationToken);

                if (user == null)
                {
                    _logger.LogWarning("Login failed - user not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Invalid email or password!");
                }

                if (!user.IsVerified)
                {
                    _logger.LogWarning("Login failed - user isn't verified");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User isn't verified");
                }

                if (user.IsDeleted)
                {
                    _logger.LogWarning("Login failed - user is deleted");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User was deleted");
                }

                if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordSalt, user.PasswordHash))
                {
                    _logger.LogWarning("Login failed - user input wrong password");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Invalid email or password!");
                }

                //_logger.LogDebug("Retrieving mention by user {UserId} from database", user.Id); 
                //var mention = await _unitOfWork.UserMentionRepository.GetByUserIdAsync(user.Id, cancellationToken);

                //if (mention == null)
                //{
                //    _logger.LogWarning("Login failed - mention not found");
                //    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                //    return Result.Fail("Mention not found");
                //}

                //user.Mention = mention;

                _logger.LogDebug("Parameters validated successfully");

                _logger.LogDebug("Generating Access and Refresh Tokens");
                var accessToken = _tokenService.GenerateAccessToken(user);
                var refreshToken = _tokenService.GenerateRefreshToken(user, accessToken);

                _logger.LogDebug("Adding refresh token to database: {RefreshTokenId}", refreshToken.Id);
                await _unitOfWork.RefreshTokenRepository.AddAsync(refreshToken, cancellationToken);

                _logger.LogDebug("Saving changes to database");
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Committing transaction");
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("User login successfully");

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login failed");

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during login");
                }

                throw;
            }
        }
    }
}
