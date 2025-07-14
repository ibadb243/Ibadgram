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

namespace Application.CQRS.Users.Commands.UpdateShortname
{
    public class UpdateShortnameCommand : IRequest<Result>
    {
        public Guid UserId { get; set; }
        public string Shortname { get; set; }
    }

    public class UpdateShortnameCommandValidator : AbstractValidator<UpdateShortnameCommand>
    {
        private readonly IUserRepository _userRepository;
        private readonly IMentionRepository _mentionRepository;

        public UpdateShortnameCommandValidator(
            IUserRepository userRepository,
            IMentionRepository mentionRepository)
        {
            _userRepository = userRepository;
            _mentionRepository = mentionRepository;

            RuleFor(x => x.UserId)
                .NotEmpty()
                    .WithMessage("UserId is required");
                //.MustAsync(BeExist)
                //.WithMessage("User not found")
                //.MustAsync(BeVerified)
                //.WithMessage("User do not pass registration");

            RuleFor(x => x.Shortname)
                .NotEmpty()
                    .WithMessage("Shortname is required")
                .MinimumLength(ShortnameConstants.MinLength)
                    .WithMessage($"Shortname's length should have minimum {ShortnameConstants.MinLength} characters")
                .MaximumLength(ShortnameConstants.MaxLength)
                    .WithMessage($"Shortname's length cann't have characters greater than {ShortnameConstants.MaxLength}");
            //.MustAsync(BeFree)
            //.WithMessage("Shortname has already taken");
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

        private async Task<bool> BeFree(string shortname, CancellationToken cancellationToken)
        {
            return await _mentionRepository.ExistsByShortnameAsync(shortname, cancellationToken);
        }
    }

    public class UpdateShortnameCommandHandler : IRequestHandler<UpdateShortnameCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdateShortnameCommandHandler> _logger;

        public UpdateShortnameCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<UpdateShortnameCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result> Handle(UpdateShortnameCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting update shortname proccess");

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Retrieving user by id {UserId} from database", request.UserId);
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);

                if (user == null)
                {
                    _logger.LogDebug("Update shortname failed - user not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail($"User with id {request.UserId} not found");
                }

                if (!user.IsVerified)
                {
                    _logger.LogDebug("Update shortname failed - user not verified");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User should pass full registration");
                }

                if (user.IsDeleted)
                {
                    _logger.LogDebug("Update shortname failed - user is deleted");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User is deleted");
                }

                if (request.Shortname == user.Mention.Shortname)
                {
                    _logger.LogDebug("Update shortname failed - same shortname");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Shortname is yours");
                }

                if (await _unitOfWork.MentionRepository.ExistsByShortnameAsync(request.Shortname, cancellationToken))
                {
                    _logger.LogDebug("Update shortname failed - shortname is not free");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Shortname has been taken");
                }

                _logger.LogInformation("Shortname validated successfully, update mention: {MentionId}", user.Mention.Id);

                var mention = user.Mention;

                mention.Shortname = request.Shortname;

                _logger.LogDebug("Saving changes to database");
                await _unitOfWork.MentionRepository.UpdateAsync(mention, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Committing transaction");
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Updating shortname completed successfully for user {UserId}", user.Id);

                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update shortname failed");

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during shortname updating");
                }

                throw;
            }
        }
    }
}
