using Application.Interfaces.Repositories;
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

namespace Application.CQRS.Users.Commands.UpdateUser
{
    public class UpdateUserCommand : IRequest<Result>
    {
        public Guid UserId { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Bio { get; set; }
    }

    public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
    {
        private readonly IUserRepository _userRepository;

        public UpdateUserCommandValidator(IUserRepository userRepository)
        {
            _userRepository = userRepository;

            RuleFor(x => x.UserId)
                .NotEmpty()
                    .WithMessage("UserId is required");
                //.MustAsync(BeExist)
                //.WithMessage("User not found")
                //.MustAsync(BeVerified)
                //.WithMessage("User do not pass registration");

            RuleFor(x => x.Firstname)
                .MinimumLength(UserConstants.FirstnameMinLength)
                    .WithMessage($"Firstname's length should have minimum {UserConstants.FirstnameMinLength} characters")
                .MaximumLength(UserConstants.FirstnameMaxLength)
                    .WithMessage($"Firstname's length cann't have characters greater than {UserConstants.FirstnameMaxLength}");

            RuleFor(x => x.Lastname)
                .MaximumLength(UserConstants.LastnameLength)
                    .WithMessage($"Lastname's length cann't have characters greater than {UserConstants.LastnameLength}");

            RuleFor(x => x.Bio)
                .MaximumLength(UserConstants.BioLength)
                    .WithMessage($"Bio's length cann't have characters greater than {UserConstants.BioLength}");

            RuleFor(x => x)
                .Must(BeAllPropertiesNull)
                    .WithMessage("There aren't any parameters");
        }

        private bool BeAllPropertiesNull(UpdateUserCommand command)
        {
            return !(command.Firstname == null && command.Lastname == null && command.Bio == null);
        }

        private async Task<bool> BeExist(Guid userId, CancellationToken cancellationToken)
        {
            return await _userRepository.ExistsAsync(userId, cancellationToken);
        }

        private async Task<bool> BeVerified(Guid userId, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            return user != null && user.IsVerified;
        }
    }

    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdateUserCommandHandler> _logger;

        public UpdateUserCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<UpdateUserCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting update user process");

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Retrieving user by id {UserId} from database", request.UserId);
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);

                if (user == null)
                {
                    _logger.LogDebug("Update user failed - user not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail($"User with id {request.UserId} not found");
                }

                if (!user.IsVerified)
                {
                    _logger.LogDebug("Update user failed - user not verified");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User should pass full registration");
                }

                if (user.IsDeleted)
                {
                    _logger.LogDebug("Update user failed - user is deleted");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User is deleted");
                }

                _logger.LogInformation("User validated successfully, update user: {UserId}", user.Id);

                _logger.LogDebug("Update user status: {UpdateStatus}", new
                {
                    FirstnameChange = request.Firstname != null,
                    LastnameChange = request.Lastname != null,
                    BioChange = request.Bio != null,
                });
                user.Firstname = request.Firstname ?? user.Firstname;
                user.Lastname = request.Lastname ?? user.Lastname;
                user.Bio = request.Bio ?? user.Bio;

                _logger.LogDebug("Saving changes to database");
                await _unitOfWork.UserRepository.UpdateAsync(user, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Committing transaction");
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Updating user data completed successfully for user {UserId}", user.Id);

                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update user failed");

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during user updating");
                }

                throw;
            }
        }
    }
}
