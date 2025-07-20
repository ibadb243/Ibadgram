using Application.CQRS.Users.Commands.UpdateUser;
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application.CQRS.Chats.Commands.UpdateGroup
{
    public class UpdateGroupCommand : IRequest<Result>
    {
        public Guid UserId { get; set; }
        public Guid GroupId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
    }

    public class UpdateGroupCommandValidator : AbstractValidator<UpdateGroupCommand>
    {
        public UpdateGroupCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                    .WithMessage("UserId is required");

            RuleFor(x => x.GroupId)
                .NotEmpty()
                    .WithMessage("GroupId is required");

            RuleFor(x => x.Name)
                .MinimumLength(ChatConstants.NameMinLength)
                    .WithMessage($"Name's length should have minimum {ChatConstants.NameMinLength} characters")
                .MaximumLength(ChatConstants.NameMaxLength)
                    .WithMessage($"Name's length cann't have characters greater than {ChatConstants.NameMaxLength}");

            RuleFor(x => x.Description)
                .MaximumLength(ChatConstants.DescriptionLength)
                    .WithMessage($"Description's length cann't have characters greater than {UserConstants.BioLength}");

            RuleFor(x => x)
                .Must(BeAllPropertiesNull)
                    .WithMessage("There aren't any parameters");
        }

        private bool BeAllPropertiesNull(UpdateGroupCommand command)
        {
            return !(command.Name == null && command.Description == null);
        }
    }

    public class UpdateGroupCommandHandler : IRequestHandler<UpdateGroupCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdateGroupCommandHandler> _logger;

        public UpdateGroupCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<UpdateGroupCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result> Handle(UpdateGroupCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting update group proccess");

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Retrieving user by id {UserId} from database", request.UserId);
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);

                if (user == null)
                {
                    _logger.LogWarning("Update group failed - user not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User not found");
                }

                if (!user.IsVerified)
                {
                    _logger.LogWarning("Update group failed - user isn't verified");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User isn't verified");
                }

                if (user.IsDeleted)
                {
                    _logger.LogWarning("Update group failed - user is deleted");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User is deleted");
                }

                _logger.LogDebug("Retrieving group by id {ChatId} from database", request.GroupId);
                var group = await _unitOfWork.ChatRepository.GetByIdAsync(request.GroupId, cancellationToken);

                if (group == null)
                {
                    _logger.LogWarning("Update group failed - group not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Group not found");
                }

                if (group.IsDeleted)
                {
                    _logger.LogWarning("Update group failed - group has already been deleted");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Group is deleted");
                }

                _logger.LogDebug("Retrieving member by (groupId:userId) ({ChatId}:{UserId}) from database", request.GroupId, request.UserId);
                var member = await _unitOfWork.ChatMemberRepository.GetByIdsAsync(group.Id, user.Id, cancellationToken);
                
                if (member == null)
                {
                    _logger.LogWarning("Delete group failed - user with id {UserId} isn't member of group with id {ChatId}", user.Id, group.Id);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User isn't member of group");
                }

                if (member.Role != ChatRole.Creator)
                {
                    _logger.LogWarning("Delete group failed - user with id {UserId} isn't creator of group wiht id {ChatId}", user.Id, group.Id);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User isn't creator of group");
                }

                _logger.LogInformation("User, Group and Member validated successfully");

                group.Name = request.Name ?? group.Name;
                group.Description = request.Description ?? group.Description;

                await _unitOfWork.ChatRepository.UpdateAsync(group, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Update group failed");

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during update group");
                }

                throw;
            }
        }
    }
}
