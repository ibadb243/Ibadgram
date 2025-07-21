using Application.Interfaces.Repositories;
using Domain.Common.Constants;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Messages.Commands.UpdateMessage
{
    public class UpdateMessageCommand : IRequest<Result>
    {
        public Guid UserId { get; set; }
        public Guid ChatId { get; set; }
        public long MessageId { get; set; }
        public string Text { get; set; }
    }

    public class UpdateMessageCommandValidator : AbstractValidator<UpdateMessageCommand>
    {
        public UpdateMessageCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                    .WithMessage("User ID is required");

            RuleFor(x => x.ChatId)
                .NotEmpty()
                    .WithMessage("Chat ID is required");

            RuleFor(x => x.MessageId)
                .GreaterThan(0)
                    .WithMessage("Message ID must be greater than 0");

            RuleFor(x => x.Text)
                .NotEmpty()
                    .WithMessage("Message content cannot be empty")
                .MinimumLength(MessageConstants.MinLength)
                    .WithMessage($"Message must be at least {MessageConstants.MinLength} characters long")
                .MaximumLength(MessageConstants.MaxLength)
                    .WithMessage($"Message cannot exceed {MessageConstants.MaxLength} characters");
        }
    }

    public class UpdateMessageCommandHandler : IRequestHandler<UpdateMessageCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdateMessageCommandHandler> _logger;

        public UpdateMessageCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<UpdateMessageCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result> Handle(UpdateMessageCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Updating message {MessageId} in chat {ChatId} by user {UserId}",
                request.MessageId, request.ChatId, request.UserId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Retrieving user by id {UserId} from database",  request.UserId);
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);

                if (user == null)
                {
                    _logger.LogWarning("Update message failed - user not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User not found");
                }

                if (!user.IsVerified)
                {
                    _logger.LogWarning("Update message failed - user isn't verified");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User isn't verified");
                }

                if (user.IsDeleted)
                {
                    _logger.LogWarning("Update message failed - user is deleted");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User is deleted");
                }

                _logger.LogDebug("Retrieving chat by id {ChatId} from database", request.ChatId);
                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(request.ChatId, cancellationToken);
                
                if (chat == null)
                {
                    _logger.LogWarning("Update message failed - chat not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Chat not found");
                }

                if (chat.IsDeleted)
                {
                    _logger.LogWarning("Update message failed - chat is deleted");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User is deleted");
                }

                _logger.LogDebug("Retrieving member by chat {ChatId} and by user {UserId} from database", request.ChatId, request.UserId);
                var member = await _unitOfWork.ChatMemberRepository.GetByIdsAsync(request.ChatId, request.UserId, cancellationToken);

                if (member == null)
                {
                    _logger.LogWarning("Update message failed - user isn't member of chat");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User isn't member of chat");
                }

                _logger.LogDebug("Retrieving messsage by chat {ChatId} and by messageId {MessageId} from database", request.ChatId, request.MessageId);
                var message = await _unitOfWork.MessageRepository.GetByIdAsync(request.ChatId, request.MessageId, cancellationToken);

                if (message == null)
                {
                    _logger.LogWarning("Update message failed - message not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Message not found");
                }

                if (message.IsDeleted)
                {
                    _logger.LogWarning("Update message failed - message is deleted");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Message is deleted");
                }

                if (message.UserId != request.UserId)
                {
                    _logger.LogWarning("Update message failed - user isn't author of message");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User isn't author of message");
                }

                _logger.LogInformation("Validation completed successfully");

                message.Text = request.Text;
                message.UpdatedAtUtc = DateTime.UtcNow;

                _logger.LogDebug("Saving changes to database");
                await _unitOfWork.MessageRepository.UpdateAsync(message, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Committing transaction");
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return Result.Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error occurred while update message from ChatId: {ChatId} MessageId: {MessagId}",
                    request.ChatId, request.MessageId);

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during update message");
                }

                throw;
            }
        }
    }
}
