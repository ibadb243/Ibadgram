using Domain.Common;
using Domain.Common.Constants;
using Domain.Entities;
using Domain.Errors;
using Domain.Repositories;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Messages.Commands.SendMessage
{
    public class SendMessageCommand : IRequest<Result<SendMessageResponse>>
    {
        public Guid UserId { get; set; }
        public Guid ChatId { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class SendMessageResponse
	{
		public long Id { get; set; }
        public DateTime Timestamp { get; set; }
	}

	public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
	{
		public SendMessageCommandValidator()
		{
			RuleFor(x => x.UserId)
				.Cascade(CascadeMode.Stop)
				.NotEmpty()
					.WithErrorCode(ErrorCodes.REQUIRED_FIELD)
					.WithMessage("UserId is required");

			RuleFor(x => x.ChatId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
					.WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("ChatId is required");

			RuleFor(x => x.Message)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("Message content is required")
				.MinimumLength(MessageConstants.MinLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_SHORT)
                    .WithMessage($"Message must be at least {MessageConstants.MinLength} characters")
				.MaximumLength(MessageConstants.MaxLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_LONG)
                    .WithMessage($"Message cannot exceed {MessageConstants.MaxLength} characters");
		}
	}

	public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, Result<SendMessageResponse>>
	{
		private readonly IUnitOfWork _unitOfWork;
		private readonly ILogger<SendMessageCommandHandler> _logger;

		public SendMessageCommandHandler(
			IUnitOfWork unitOfWork,
			ILogger<SendMessageCommandHandler> logger)
		{
			_unitOfWork = unitOfWork;
			_logger = logger;
		}

		public async Task<Result<SendMessageResponse>> Handle(
			SendMessageCommand request,
			CancellationToken cancellationToken)
		{
            _logger.LogInformation(
                "Starting send message process from user: {UserId} to chat: {ChatId}",
                request.UserId, request.ChatId);

			await _unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);

			try
			{
				var userValidationResult = await GetAndValidateUserAsync(request.UserId, cancellationToken);
				if (userValidationResult.IsFailed)
				{
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return userValidationResult.ToResult();
                }

				var user = userValidationResult.Value;

				var chatValidationResult = await GetAndValidateChatAsync(request.ChatId, cancellationToken);
				if (chatValidationResult.IsFailed)
				{
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return chatValidationResult.ToResult();
                }

				var chat = chatValidationResult.Value;

				var membershipValidationResult = await ValidateMembershipAsync(chat, user, cancellationToken);
				if (membershipValidationResult.IsFailed)
				{
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return membershipValidationResult.ToResult();
                }

				var message = new Message
				{
					ChatId = request.ChatId,
					UserId = request.UserId,
					Text = request.Message,
					CreatedAtUtc = DateTime.UtcNow,
					IsDeleted = false
				};

                await _unitOfWork.MessageRepository.AddAsync(message, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Message created successfully: {ChatId} {MessageId} by user: {UserId}",
					message.ChatId, message.Id, message.UserId);

				return Result.Ok(new SendMessageResponse
				{
					Id = message.Id,
                    Timestamp = message.CreatedAtUtc,
				});
			}
			catch (Exception ex)
			{
				_logger.LogError(ex,
					"Send message failed for user: {UserId} in chat: {ChatId}",
					request.UserId, request.ChatId);

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction");
                }

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to send message due to system error"
                ));
            }
		}

		private async Task<Result<User>> GetAndValidateUserAsync(
            Guid userId,
            CancellationToken cancellationToken)
		{
            _logger.LogDebug("Retrieving user by ID: {UserId}", userId);

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken);

			if (user == null)
			{
                _logger.LogWarning("Message sending failed - user not found: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_FOUND,
                    "User not found",
                    new { UserId = userId }
                ));
            }

			if (!user.IsVerified)
			{
                _logger.LogWarning("Message sending failed - account hasn't completed for user: {UserId}", userId);

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_VERIFIED,
                    "User not verified",
                    new
                    {
                        UserId = userId,
                    }
                ));
            }

            if (user.IsDeleted)
            {
                _logger.LogWarning("Message sending failed - account is deleted for user: {UserId}", userId);

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_DELETED,
                    "User is deleted",
                    new
                    {
                        UserId = userId,
                    }
                ));
            }

            _logger.LogDebug("User validation successful: {UserId}", userId);
            return Result.Ok(user);
		}

		private async Task<Result<Chat>> GetAndValidateChatAsync(
            Guid chatId, 
            CancellationToken cancellationToken)
		{
			_logger.LogDebug("Retrieving chat by ID: {ChatId}", chatId);

			var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId, cancellationToken);

			if (chat == null)
			{
                _logger.LogWarning("Message sending failed - chat not found: {ChatId}", chatId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CHAT_NOT_FOUND,
                    "Chat not found",
                    new { ChatId = chatId }
                ));
            }

			if (chat.IsDeleted)
			{
                _logger.LogWarning("Message sending failed - chat is deleted: {ChatId}", chatId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CHAT_DELETED,
                    "Chat is deleted",
                    new { ChatId = chatId }
                ));
            }

            _logger.LogDebug("Chat validation successful: {ChatId}", chatId);
            return Result.Ok(chat);
		}

		private async Task<Result<ChatMember>> ValidateMembershipAsync(
			Chat chat,
			User user,
			CancellationToken cancellationToken)
		{
            _logger.LogDebug("Validating membership for user {UserId} in chat {ChatId}", user.Id, chat.Id);

            var member = await _unitOfWork.ChatMemberRepository.GetByIdsAsync(
				chat.Id, user.Id, cancellationToken);

			if (member == null)
			{
                _logger.LogWarning("Message sending failed - user is not a member of the chat: UserId {UserId}, ChatId {ChatId}",
                    user.Id, chat.Id);

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CHAT_ACCESS_DENIED,
                    "You are not a member of this chat",
                    new
                    {
                        UserId = user.Id,
                        ChatId = chat.Id,
                        SuggestedAction = "Join the chat or request access"
                    }
                ));
            }

            _logger.LogDebug("Membership validation successful for user {UserId} in chat {ChatId}", user.Id, chat.Id);
            return Result.Ok(member);
		}
	}
}
