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

namespace Application.CQRS.Users.Commands.CompleteAccount
{
    public class CompleteAccountCommand : IRequest<Result<Guid>>
    {
        public Guid UserId { get; set; }
        public string Shortname { get; set; }
        public string? Bio { get; set; }
    }

    public class CompleteAccountCommandValidator : AbstractValidator<CompleteAccountCommand>
    {
        private readonly IMentionRepository _mentionRepository;

        public CompleteAccountCommandValidator(IMentionRepository mentionRepository)
        {
            _mentionRepository = mentionRepository;

            RuleFor(x => x.UserId)
                .NotEmpty()
                    .WithMessage("UserId is required");

            RuleFor(x => x.Shortname)
                .NotEmpty()
                    .WithMessage("Shortname is required")
                .MinimumLength(ShortnameConstants.MinLength)
                    .WithMessage($"Shortname's length should have minimum {ShortnameConstants.MinLength} characters")
                .MaximumLength(ShortnameConstants.MaxLength)
                    .WithMessage($"Shortname's length cann't have characters greater than {ShortnameConstants.MinLength}");
                //.MustAsync(BeUniqueShortname)
                //.WithMessage($"Shortname has already taken");

            RuleFor(x => x.Bio)
                .MaximumLength(UserConstants.BioLength)
                    .WithMessage($"Bio's length cann't have characters greater than {UserConstants.BioLength}");
        }

        private async Task<bool> BeUniqueShortname(string shortname, CancellationToken cancellationToken)
        {
            return !await _mentionRepository.ExistsByShortnameAsync(shortname, cancellationToken);
        }
    }

    public class CompleteAccountCommandHandler : IRequestHandler<CompleteAccountCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CompleteAccountCommandHandler> _logger;

        public CompleteAccountCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<CompleteAccountCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CompleteAccountCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting complete account proccess");

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Retrieving user by id {UserId} from database", request.UserId);
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);

                if (user == null)
                {
                    _logger.LogWarning("Complete account failed - user not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail($"User with id {request.UserId} not found");
                }

                if (!user.EmailConfirmed)
                {
                    _logger.LogWarning("Complete account failed - user's email address not confirmed");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User's email address not confirmed");
                }

                _logger.LogDebug("User confirmation status validated successfully, updating user: {UserId}", user.Id);

                user.Bio = request.Bio;
                user.IsVerified = true;
                
                await _unitOfWork.UserRepository.UpdateAsync(user, cancellationToken);

                var mention = new UserMention
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Shortname = request.Shortname,
                };

                _logger.LogDebug("Adding mention to database: {MentionId}", mention.Id);
                await _unitOfWork.MentionRepository.AddAsync(mention, cancellationToken);

                _logger.LogDebug("Saving user changes to database");
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Committing transaction");
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("User completed yours account: {UserId}", user.Id);

                return user.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Account completing failed");

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during complete account");
                }

                throw;
            }
        }
    }
}
