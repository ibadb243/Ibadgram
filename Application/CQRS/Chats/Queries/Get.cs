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

namespace Application.CQRS.Chats.Queries
{
    public abstract class ChatVm
    {
        public bool IsDeleted { get; set; }
        public ChatType Type { get; set; }
    }

    public class PersonalChatVm : ChatVm
    {
        public int MessageCount { get; set; }
    }

    public class OneToOneChatVm : ChatVm
    {
        public string Firstname { get; set; }
        public string? Lastname { get; set; }
        public string Description { get; set; }
        public string Shortname { get; set; }
        public bool IsOtherUserDeleted { get; set; }
    }

    public class GroupChatVm : ChatVm
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Shortname { get; set; }
        public int MemberCount { get; set; }
        public bool IsPrivate { get; set; }
    }

    public class DeletedChatVm : ChatVm
    {
        public DeletedChatVm()
        {
            IsDeleted = true;
        }
    }

    public class GetChatQuery : IRequest<Result<ChatVm>>
    {
        public Guid UserId { get; set; }
        public Guid ChatId { get; set; }
    }

    public class GetChatQueryValidator : AbstractValidator<GetChatQuery>
    {
        public GetChatQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                    .WithMessage("UserId is required");

            RuleFor(x => x.ChatId)
                .NotEmpty()
                    .WithMessage("ChatId is required");
        }
    }

    public class GetChatQueryHandler : IRequestHandler<GetChatQuery, Result<ChatVm>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IChatMemberRepository _chatMemberRepository;
        private readonly ILogger<GetChatQueryHandler> _logger;

        public GetChatQueryHandler(
            IUserRepository userRepository,
            IChatRepository chatRepository,
            IChatMemberRepository chatMemberRepository,
            ILogger<GetChatQueryHandler> logger)
        {
            _userRepository = userRepository;
            _chatRepository = chatRepository;
            _chatMemberRepository = chatMemberRepository;
            _logger = logger;
        }

        public async Task<Result<ChatVm>> Handle(GetChatQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting get chat process for UserId: {UserId}, ChatId: {ChatId}",
                request.UserId, request.ChatId);

            var userValidationResult = await ValidateUserAsync(request.UserId, cancellationToken);
            if (userValidationResult.IsFailed)
                return userValidationResult.ToResult();

            var user = userValidationResult.Value;

            var chatValidationResult = await ValidateChatAsync(request.ChatId, cancellationToken);
            if (chatValidationResult.IsFailed)
                return chatValidationResult.ToResult();

            var chat = chatValidationResult.Value;

            if (chat.IsDeleted)
            {
                _logger.LogInformation("Chat {ChatId} is deleted", request.ChatId);
                return Result.Ok<ChatVm>(new DeletedChatVm());
            }

            var accessResult = await ValidateAccessAsync(chat, user, cancellationToken);
            if (accessResult.IsFailed)
                return accessResult;

            var chatVm = await CreateChatViewModelAsync(chat, user, cancellationToken);

            _logger.LogInformation("Successfully retrieved chat {ChatId} for user {UserId}",
                request.ChatId, request.UserId);

            return Result.Ok(chatVm);
        }

        private async Task<Result<User>> ValidateUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Validating user {UserId}", userId);

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found", userId);
                return Result.Fail("User not found");
            }

            if (!user.IsVerified)
            {
                _logger.LogWarning("User {UserId} is not verified", userId);
                return Result.Fail("User is not verified");
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

            var chat = await _chatRepository.GetByIdAsync(chatId, cancellationToken);

            if (chat == null)
            {
                _logger.LogWarning("Chat {ChatId} not found", chatId);
                return Result.Fail("Chat not found");
            }

            return Result.Ok(chat);
        }

        private async Task<Result> ValidateAccessAsync(Chat chat, User user, CancellationToken cancellationToken)
        {
            if (chat.Type == ChatType.Group && chat.IsPrivate.HasValue && !chat.IsPrivate.Value)
                return Result.Ok();

            _logger.LogDebug("Checking access for user {UserId} to chat {ChatId}", user.Id, chat.Id);

            var member = await _chatMemberRepository.GetByIdsAsync(chat.Id, user.Id, cancellationToken);

            if (member == null)
            {
                _logger.LogWarning("Access denied: User {UserId} is not a member of chat {ChatId}",
                    user.Id, chat.Id);
                return Result.Fail("Access denied");
            }

            return Result.Ok();
        }

        private async Task<ChatVm> CreateChatViewModelAsync(Chat chat, User user, CancellationToken cancellationToken)
        {
            return chat.Type switch
            {
                ChatType.Personal => CreatePersonalChatVm(chat),
                ChatType.OneToOne => await CreateOneToOneChatVmAsync(chat, user),
                ChatType.Group => CreateGroupChatVm(chat),
                _ => new DeletedChatVm()
            };
        }

        private static PersonalChatVm CreatePersonalChatVm(Chat chat)
        {
            return new PersonalChatVm
            {
                Type = chat.Type,
                IsDeleted = chat.IsDeleted,
                MessageCount = chat.Messages?.Count() ?? 0
            };
        }

        private async Task<ChatVm> CreateOneToOneChatVmAsync(Chat chat, User currentUser)
        {
            var otherMember = chat.Members?.FirstOrDefault(m => m.UserId != currentUser.Id);

            if (otherMember?.User == null)
            {
                _logger.LogError("OneToOne chat {ChatId} has invalid member structure", chat.Id);
                return new DeletedChatVm();
            }

            // Если собеседник удален, возвращаем специальную версию
            if (otherMember.User.IsDeleted)
            {
                return new OneToOneChatVm
                {
                    Type = chat.Type,
                    IsDeleted = false,
                    IsOtherUserDeleted = true,
                    Firstname = "Deleted User",
                    Lastname = null,
                    Description = "This user has been deleted",
                    Shortname = "deleted_user"
                };
            }

            return new OneToOneChatVm
            {
                Type = chat.Type,
                IsDeleted = chat.IsDeleted,
                IsOtherUserDeleted = false,
                Firstname = otherMember.User.Firstname,
                Lastname = otherMember.User.Lastname,
                Description = otherMember.User.Bio ?? "No description",
                Shortname = otherMember.User.Mention?.Shortname ?? "unknown"
            };
        }

        private static GroupChatVm CreateGroupChatVm(Chat chat)
        {
            return new GroupChatVm
            {
                Type = chat.Type,
                IsDeleted = chat.IsDeleted,
                Name = chat.Name,
                Description = chat.Description ?? "No description",
                Shortname = chat.Mention?.Shortname ?? "unknown",
                MemberCount = chat.Members?.Count() ?? 0,
                IsPrivate = chat.IsPrivate.Value
            };
        }
    }
}
