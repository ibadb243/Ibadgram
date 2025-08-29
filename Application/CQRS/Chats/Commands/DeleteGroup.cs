using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Repositories;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Chats.Commands.DeleteGroup
{
    public class DeleteGroupCommand : IRequest<Result>
    {
        public Guid UserId { get; set; }
        public Guid GroupId { get; set; }
    }

    public class DeleteGroupCommandValidator : AbstractValidator<DeleteGroupCommand>
    {
        public DeleteGroupCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("UserId is required");

            RuleFor(x => x.GroupId)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("GroupId is required");
        }
    }

    public class DeleteGroupCommandHandler : IRequestHandler<DeleteGroupCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeleteGroupCommandHandler> _logger;

        public DeleteGroupCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<DeleteGroupCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result> Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting group deletion proccess for user: {UserId}, group: {ChatId}",
                request.UserId, request.GroupId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);

            try
            {
                var userResult = await GetAndValidateUserAsync(request.UserId, cancellationToken);
                if (userResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return userResult.ToResult();
                }

                var user = userResult.Value;

                var chatResult = await GetAndValidateChatAsync(request.GroupId, cancellationToken);
                if (chatResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return chatResult.ToResult();
                }

                var chat = chatResult.Value;

                var accessResult = await ValidateChatMemberAndPermissionsAsync(user.Id, chat.Id, cancellationToken);
                if (accessResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return accessResult;
                }

                await DeleteGroupAsync(chat, cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Group deleted successfully: {ChatId} by user: {UserId}",
                    chat.Id, user.Id);

                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Group deletion failed for user: {UserId}", request.UserId);

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during group deletion");
                }

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to delete group due to system error"
                ));
            }
        }

        public async Task<Result<User>> GetAndValidateUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving user by ID: {UserId}", userId);

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Group deletion failed - user not found: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_FOUND,
                    "User not found",
                    new { UserId = userId }
                ));
            }

            if (!user.IsVerified)
            {
                _logger.LogWarning("Group deletion failed - user not verified: {UserId}", user.Id);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_VERIFIED,
                    "User account is not verified",
                    new
                    {
                        UserId = user.Id,
                        SuggestedAction = "Please verify your account first"
                    }
                ));
            }

            if (user.IsDeleted)
            {
                _logger.LogWarning("Group deletion failed - user is deleted: {UserId}", user.Id);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_DELETED,
                    "User account has been deleted",
                    new { UserId = user.Id }
                ));
            }

            _logger.LogDebug("User validation successful: {UserId}", user.Id);
            return Result.Ok(user);
        }

        public async Task<Result<Chat>> GetAndValidateChatAsync(Guid chatId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving chat by ID: {ChatId}", chatId);

            var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId, cancellationToken);

            if (chat == null)
            {
                _logger.LogWarning("Group deletion failed - chat not found: {ChatId}", chatId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CHAT_NOT_FOUND,
                    "Chat not found",
                    new { ChatId = chatId }
                ));
            }

            if (chat.Type == ChatType.Personal)
            {
                _logger.LogWarning("Group deletion failed - personal chat: {ChatId}", chatId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CHAT_ACCESS_DENIED,
                    "Perrsonal chat couldn't be deleted",
                    new { ChatId = chatId }
                ));
            }

            if (chat.IsDeleted)
            {
                _logger.LogWarning("Group deletion failed - chat was deleted: {ChatId}", chatId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CHAT_NOT_FOUND,
                    "Chat was deleted"
                ));
            }

            _logger.LogDebug("Chat validation successful: {ChatId}", chat.Id);
            return chat;
        }

        public async Task<Result> ValidateChatMemberAndPermissionsAsync(Guid userId, Guid chatId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving chat member by Ids: {ChatId} {UserId}", chatId, userId);

            var member = await _unitOfWork.ChatMemberRepository.GetByIdsAsync(chatId, userId, cancellationToken);

            if (member == null)
            {
                _logger.LogWarning("Group deletion failed - member not found");
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CHAT_ACCESS_DENIED,
                    "You aren't member"
                ));
            }

            if (member.Role != ChatRole.Creator)
            {
                _logger.LogWarning("Group deletion failed - member isn't creator");
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CHAT_ACCESS_DENIED,
                    "You hasn't access"
                ));
            }

            return Result.Ok();
        }

        public async Task DeleteGroupAsync(Chat chat, CancellationToken cancellationToken)
        {
            await _unitOfWork.ChatRepository.DeleteAsync(chat, cancellationToken);

            _logger.LogDebug("Group successfully was deleted");
        }
    }
}
