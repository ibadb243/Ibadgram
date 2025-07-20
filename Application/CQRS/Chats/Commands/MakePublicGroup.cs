using Application.Interfaces.Repositories;
using Domain.Common;
using Domain.Common.Constants;
using Domain.Entities;
using Domain.Enums;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Chats.Commands.MakePublicGroup
{
    public class MakePublicGroupCommand : IRequest<Result>
    {
        public Guid UserId { get; set; }
        public Guid GroupId { get; set; }
        public string Shortname { get; set; }
    }

    public class MakePublicGroupCommandValidator : AbstractValidator<MakePublicGroupCommand>
    {
        public MakePublicGroupCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                    .WithMessage("UserId is required");

            RuleFor(x => x.GroupId)
                .NotEmpty()
                    .WithMessage("GroupId is required");

            RuleFor(x => x.Shortname)
                .NotEmpty()
                    .WithMessage("Shortname is required")
                .MinimumLength(ShortnameConstants.MinLength)
                    .WithMessage($"Shortname's length should have minimum {ShortnameConstants.MinLength} characters")
                .MaximumLength(ShortnameConstants.MaxLength)
                    .WithMessage($"Shortname's length cann't have characters greater than {ShortnameConstants.MaxLength}");
        }
    }

    public class MakePublicGroupCommandHandler : IRequestHandler<MakePublicGroupCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<MakePublicGroupCommandHandler> _logger;

        public MakePublicGroupCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<MakePublicGroupCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result> Handle(MakePublicGroupCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting make public group proccess");

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

                if (group.IsPrivate.HasValue && !group.IsPrivate.Value)
                {
                    _logger.LogDebug("Configure group failed - group is public");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Group is public");
                }

                if (await _unitOfWork.MentionRepository.ExistsByShortnameAsync(request.Shortname, cancellationToken))
                {
                    _logger.LogDebug("Configure group failed - shortname has already been taken");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Shortname has already been taken");
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

                _logger.LogInformation("User, Group ,Member and Shortname validated successfully");

                group.IsPrivate = true;

                var mention = new ChatMention
                {
                    Id = Guid.NewGuid(),
                    ChatId = group.Id,
                    Shortname = request.Shortname,
                    Chat = group,
                };

                _logger.LogDebug("Adding mention with id {MentionId} from database", mention.Id);
                await _unitOfWork.MentionRepository.AddAsync(mention, cancellationToken);

                _logger.LogDebug("Saving changes to databse");
                await _unitOfWork.ChatRepository.UpdateAsync(group, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Committing transaction");
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Make public group completed successfully");

                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Make public group failed");

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
