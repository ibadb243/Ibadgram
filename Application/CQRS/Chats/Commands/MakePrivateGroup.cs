using Application.Interfaces.Repositories;
using Domain.Enums;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Chats.Commands.MakePrivateGroup
{
    public class MakePrivateGroupCommand : IRequest<Result>
    {
        public Guid UserId { get; set; }
        public Guid GroupId { get; set; }
    }

    public class MakePrivateGroupCommandValidator : AbstractValidator<MakePrivateGroupCommand>
    {
        public MakePrivateGroupCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                    .WithMessage("UserId is required");

            RuleFor(x => x.GroupId)
                .NotEmpty()
                    .WithMessage("GroupId is required");
        }
    }

    public class MakePrivateGroupCommandHandler : IRequestHandler<MakePrivateGroupCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MakePrivateGroupCommandHandler> _logger;

        public MakePrivateGroupCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<MakePrivateGroupCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result> Handle(MakePrivateGroupCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting make private group proccess");

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Retrieving user by id {UserId} from database", request.UserId);
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);

                if (user == null)
                {
                    _logger.LogDebug("Configure group failed - user not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User not found");
                }

                if (!user.IsVerified)
                {
                    _logger.LogDebug("Configure group failed - user isn't verified");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User isn't verified");
                }

                if (user.IsDeleted)
                {
                    _logger.LogDebug("Configure group failed - user is deleted");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User is deleted");
                }

                _logger.LogDebug("Retrieving group by id {ChatId} from database", request.GroupId);
                var group = await _unitOfWork.ChatRepository.GetByIdAsync(request.GroupId, cancellationToken);

                if (group == null)
                {
                    _logger.LogDebug("Configure group failed - group not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Group not found");
                }

                if (group.IsDeleted)
                {
                    _logger.LogDebug("Configure group failed - group is deleted");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Group is deleted");
                }

                if (group.IsPrivate.HasValue && group.IsPrivate.Value)
                {
                    _logger.LogDebug("Configure group failed - group is private");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Group is private");
                }

                _logger.LogDebug("Retrieving member by (groupId:userId) ({ChatId}:{UserId}) from database", request.GroupId, request.UserId);
                var member = await _unitOfWork.ChatMemberRepository.GetByIdsAsync(group.Id, user.Id, cancellationToken);

                if (member == null)
                {
                    _logger.LogWarning("Configure group failed - user with id {UserId} isn't member of group with id {ChatId}", user.Id, group.Id);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User isn't member of group");
                }

                if (member.Role != ChatRole.Creator)
                {
                    _logger.LogWarning("Configure group failed - user with id {UserId} isn't creator of group with id {ChatId}", user.Id, group.Id);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User isn't creator of group");
                }

                _logger.LogInformation("User, Group and Member validated successfully");

                group.IsPrivate = false;

                _logger.LogDebug("Deleting mention with id {MentionId} from database", group.Mention.Id);
                await _unitOfWork.MentionRepository.DeleteAsync(group.Mention.Id);

                _logger.LogDebug("Saving changes to databse");
                await _unitOfWork.ChatRepository.UpdateAsync(group, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Committing transaction");
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Make private group completed successfully");

                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Make private group failed");

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during configure group");
                }

                throw;
            }
        }
    }
}
