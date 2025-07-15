using Application.Interfaces.Repositories;
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
using System.Threading.Tasks;

namespace Application.CQRS.Chats.Commands.CreateChat
{
    public class CreateChatCommand : IRequest<Result<Guid>>
    {
        public Guid FirstUserId { get; set; }
        public Guid SecondUserId { get; set; }
    }

    public class CreateChatCommandValidator : AbstractValidator<CreateChatCommand>
    {
        public CreateChatCommandValidator()
        {
            RuleFor(x => x.FirstUserId)
                .NotEmpty()
                    .WithMessage("FirstUserId is required");

            RuleFor(x => x.SecondUserId)
                .NotEmpty()
                    .WithMessage("SecondUserId is required");

            RuleFor(x => x)
                .Must(BeNotEqual)
                    .WithMessage("FirstUserId and SecondUserId shouldn't be equal");
        }

        private bool BeNotEqual(CreateChatCommand command)
        {
            return !(command.FirstUserId == command.SecondUserId);
        }
    }

    public class CreateChatCommandHandler : IRequestHandler<CreateChatCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateChatCommandHandler> _logger;

        public CreateChatCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<CreateChatCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<Guid>> Handle(CreateChatCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start creating chat between user-1 {FirstUserId} and user-2 {SecondUser2}", request.FirstUserId, request.SecondUserId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Retrieving first user by id {UserId} from database", request.FirstUserId);
                var user1 = await _unitOfWork.UserRepository.GetByIdAsync(request.FirstUserId, cancellationToken);

                if (user1 == null)
                {
                    _logger.LogWarning("Create chat failed - first user not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("First user not found");
                }

                if (!user1.IsVerified)
                {
                    _logger.LogWarning("Create chat failed - first user isn't verified");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("First user not verified");
                }

                if (user1.IsDeleted)
                {
                    _logger.LogWarning("Create chat failed - first user is deleted");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("First user was deleted");
                }

                _logger.LogDebug("Retrieving second user by id {UserId} from database", request.SecondUserId);
                var user2 = await _unitOfWork.UserRepository.GetByIdAsync(request.SecondUserId, cancellationToken);

                if (user2 == null)
                {
                    _logger.LogWarning("Create chat failed - second user not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Second user not found");
                }

                if (!user2.IsVerified)
                {
                    _logger.LogWarning("Create chat failed - second user isn't verified");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Second user not verified");
                }

                if (user2.IsDeleted)
                {
                    _logger.LogWarning("Create chat failed - second user is deleted");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Second user was deleted");
                }

                _logger.LogDebug("Retrieving chat between user-1 {FirstUserId} and user-2 {SecondUser2} from database", request.FirstUserId, request.SecondUserId);
                var chat = await _unitOfWork.ChatRepository.FindOneToOneChatAsync(user1.Id, user2.Id, cancellationToken);

                if (chat != null)
                {
                    _logger.LogWarning("Create chat failed - chat has already been created: {ChatId}", chat.Id);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("Chat has already been created");
                }

                _logger.LogInformation("The check of users and chat validated successfully");

                chat = new Chat
                {
                    Id = Guid.NewGuid(),
                    Type = ChatType.OneToOne,
                };

                _logger.LogDebug("Adding chat with id {ChatId} to database", chat.Id);
                await _unitOfWork.ChatRepository.AddAsync(chat, cancellationToken);

                var member1 = new ChatMember
                {
                    ChatId = chat.Id,
                    UserId = user1.Id,
                    Chat = chat,
                    User = user1,
                };

                var member2 = new ChatMember
                {
                    ChatId = chat.Id,
                    UserId = user2.Id,
                    Chat = chat,
                    User = user2,
                };

                _logger.LogDebug("Adding members with users' id ({FirstUserId}:{SecondUserId}) to database", user1.Id, user2.Id);
                await _unitOfWork.ChatMemberRepository.AddAsync(member1, cancellationToken);
                await _unitOfWork.ChatMemberRepository.AddAsync(member2, cancellationToken);

                _logger.LogDebug("Saving user changes to database");
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Committing transaction");
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Creating chat with id {ChatId} completed successfully for users ({FirstUserId}:{SecondUserId})", chat.Id, user1.Id, user2.Id);

                return chat.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Creating chat failed");

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during create chat");
                }

                throw;
            }
        }
    }
}
