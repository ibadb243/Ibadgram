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

namespace Application.CQRS.Messages.Commands.SendMessage
{
	public class SendMessageResponse
	{
		public Guid ChatId { get; set; }
		public long Id { get; init; }
		public DateTime SentAt { get; init; }
		public bool IsSuccess { get; init; }

		public SendMessageResponse(Guid chatId, long id, DateTime sentAt)
		{
			ChatId = chatId;
			Id = id;
			SentAt = sentAt;
			IsSuccess = true;
		}
	}

	public class SendMessageCommand : IRequest<Result<SendMessageResponse>> 
	{
		public Guid UserId { get; set; }
		public Guid ChatId { get; set; }
		public string Message { get; set; } = string.Empty;
	}

	public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
	{
		public SendMessageCommandValidator()
		{
			RuleFor(x => x.UserId)
				.NotEmpty()
					.WithMessage("UserId is required");

			RuleFor(x => x.ChatId)
				.NotEmpty()
					.WithMessage("ChatId is required");

			RuleFor(x => x.Message)
				.NotEmpty()
					.WithMessage("Message content is required")
				.MinimumLength(MessageConstants.MinLength)
					.WithMessage($"Message must be at least {MessageConstants.MinLength} characters")
				.MaximumLength(MessageConstants.MaxLength)
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
				"Processing send message command for UserId: {UserId}, ChatId: {ChatId}",
				request.UserId, request.ChatId);

			await _unitOfWork.BeginTransactionAsync(cancellationToken);

			try
			{
				var userValidationResult = await ValidateUserAsync(request.UserId, cancellationToken);
				if (userValidationResult.IsFailed)
				{
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return userValidationResult.ToResult<SendMessageResponse>();
                }

				var user = userValidationResult.Value;

				var chatValidationResult = await ValidateChatAsync(request.ChatId, cancellationToken);
				if (chatValidationResult.IsFailed)
				{
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return chatValidationResult.ToResult<SendMessageResponse>();
                }

				var chat = chatValidationResult.Value;

				var membershipValidationResult = await ValidateMembershipAsync(chat, user, cancellationToken);
				if (membershipValidationResult.IsFailed)
				{
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return membershipValidationResult.ToResult<SendMessageResponse>();
                }

				var now = DateTime.UtcNow;
				var message = new Message
				{
					ChatId = request.ChatId,
					UserId = request.UserId,
					Text = request.Message,
					CreatedAtUtc = now,
					IsDeleted = false
				};

				_logger.LogDebug("Adding message to database");
				await _unitOfWork.MessageRepository.AddAsync(message, cancellationToken);

				_logger.LogDebug("Saving changes to database");
				await _unitOfWork.SaveChangesAsync(cancellationToken);

				_logger.LogDebug("Message created successfully with ID {MessageId}", message.Id);

				_logger.LogDebug("Committing transaction");
				await _unitOfWork.CommitTransactionAsync(cancellationToken);

				_logger.LogInformation(
					"Message sent successfully. MessageId: {MessageId}, UserId: {UserId}, ChatId: {ChatId}",
					message.Id, request.UserId, request.ChatId);

				return Result.Ok(new SendMessageResponse(message.ChatId, message.Id, now));
			}
			catch (Exception ex)
			{
				_logger.LogError(ex,
					"Error occurred while sending message for UserId: {UserId}, ChatId: {ChatId}",
					request.UserId, request.ChatId);

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during send message");
                }

                throw;
            }
		}

		private async Task<Result<User>> ValidateUserAsync(Guid userId, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Validating user {UserId}", userId);

			var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken);

			if (user == null)
			{
				_logger.LogWarning("User {UserId} not found", userId);
				return Result.Fail("User not found");
			}

			if (!user.IsVerified)
			{
				_logger.LogWarning("User {UserId} isn't verified", userId);
				return Result.Fail("User isn't verified");
			}

			if (user.IsDeleted)
			{
				_logger.LogWarning("User {UserId} is deleted", userId);
				return Result.Fail("User is deleted");
			}

			return Result.Ok(user);
		}

		private async Task<Result<Chat>> ValidateChatAsync(Guid chatId, CancellationToken cancellationToken)
		{
			_logger.LogDebug("Validating chat {ChatId}", chatId);

			var chat = await _unitOfWork.ChatRepository.GetByIdAsync(chatId, cancellationToken);

			if (chat == null)
			{
				_logger.LogWarning("Chat {ChatId} not found", chatId);
				return Result.Fail("Chat not found");
			}

			if (chat.IsDeleted)
			{
				_logger.LogWarning("Chat {ChatId} is deleted", chatId);
				return Result.Fail("Chat is deleted");
			}

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
				_logger.LogWarning("User {UserId} is not a member of chat {ChatId}", user.Id, chat.Id);
				return Result.Fail("You are not a member of this chat");
			}

			//if (member.IsDeleted)
			//{
			//    _logger.LogWarning("User {UserId} membership in chat {ChatId} is deleted", user.Id, chat.Id);
			//    return Result.Fail("Your membership has been revoked");
			//}

			return Result.Ok(member);
		}
	}
}
