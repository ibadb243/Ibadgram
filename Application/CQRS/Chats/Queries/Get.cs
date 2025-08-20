using Application.Interfaces.Repositories;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
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
        public Guid Id { get; set; }
        public ChatType Type { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class PersonalChatVm : ChatVm
    {
        public int MessageCount { get; set; }
    }

    public class OneToOneChatVm : ChatVm
    {
        public string Firstname { get; set; } = string.Empty;
        public string? Lastname { get; set; }
        public string Bio { get; set; } = string.Empty;
        public string Shortname { get; set; } = string.Empty;
        public bool IsOtherUserDeleted { get; set; }
    }

    public class GroupChatVm : ChatVm
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Shortname { get; set; } = string.Empty;
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
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("UserId is required");

            RuleFor(x => x.ChatId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("ChatId is required");
        }
    }

    public class GetChatQueryHandler : IRequestHandler<GetChatQuery, Result<ChatVm>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IChatMemberRepository _chatMemberRepository;
        private readonly IUserMentionRepository _userMentionRepository;
        private readonly IChatMentionRepository _chatMentionRepository;
        private readonly ILogger<GetChatQueryHandler> _logger;

        public GetChatQueryHandler(
            IUserRepository userRepository,
            IChatRepository chatRepository,
            IChatMemberRepository chatMemberRepository,
            IUserMentionRepository userMentionRepository,
            IChatMentionRepository chatMentionRepository,
            ILogger<GetChatQueryHandler> logger)
        {
            _userRepository = userRepository;
            _chatRepository = chatRepository;
            _chatMemberRepository = chatMemberRepository;
            _userMentionRepository = userMentionRepository;
            _chatMentionRepository = chatMentionRepository;
            _logger = logger;
        }

        public async Task<Result<ChatVm>> Handle(GetChatQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting chat retrieval process for user: {UserId}, ChatId: {ChatId}",
                request.UserId, request.ChatId);

            try
            {
                var userValidationResult = await GetAndValidateUserAsync(request.UserId, cancellationToken);
                if (userValidationResult.IsFailed)
                    return userValidationResult.ToResult();

                var user = userValidationResult.Value;

                var chatValidationResult = await GetAndValidateChatAsync(request.ChatId, cancellationToken);
                if (chatValidationResult.IsFailed)
                    return chatValidationResult.ToResult();

                var chat = chatValidationResult.Value;

                if (chat.IsDeleted)
                {
                    _logger.LogInformation("Chat is deleted, returning deleted chat view model");
                    return Result.Ok<ChatVm>(new DeletedChatVm
                    {
                        Id = chat.Id,
                        Type = chat.Type,
                        CreatedAtUtc = chat.CreatedAtUtc
                    });
                }

                var accessResult = await ValidateUserAccessAsync(chat, user, cancellationToken);
                if (accessResult.IsFailed)
                    return accessResult;

                var chatVm = await CreateChatViewModelAsync(chat, user, cancellationToken);

                _logger.LogInformation("Chat retrieved successfully");
                return Result.Ok(chatVm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat retrieval failed for user: {UserId}",
                    request.UserId);

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to create chat due to system error"
                ));
            }
        }

        private async Task<Result<User>> GetAndValidateUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving user by ID: {UserId}", userId);

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Chat retrieval failed - user not found: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_FOUND,
                    "User not found",
                    new { UserId = userId }
                ));
            }

            if (user.IsDeleted)
            {
                _logger.LogWarning("Chat retrieval failed - user is deleted: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_DELETED,
                    "User account has been deleted"
                ));
            }

            if (!user.IsVerified)
            {
                _logger.LogWarning("Chat retrieval failed - user is not verified: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_VERIFIED,
                    "User account is not verified",
                    new
                    {
                        UserId = userId,
                        SuggestedAction = "Await complete account verification"
                    }
                ));
            }

            _logger.LogDebug("User validation successful: {UserId}", userId);
            return Result.Ok(user);
        }

        private async Task<Result<Chat>> GetAndValidateChatAsync(Guid chatId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving chat by ID: {ChatId}", chatId);

            var chat = await _chatRepository.GetByIdAsync(chatId, cancellationToken);

            if (chat == null)
            {
                _logger.LogWarning("Chat retrieval failed - chat not found: {ChatId}", chatId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CHAT_NOT_FOUND,
                    "Chat not found",
                    new { ChatId = chatId }
                ));
            }

            _logger.LogDebug("Chat validation successful: {ChatId}", chatId);
            return Result.Ok(chat);
        }

        private async Task<Result> ValidateUserAccessAsync(Chat chat, User user, CancellationToken cancellationToken)
        {
            if (chat.Type == ChatType.Group && chat.IsPrivate.HasValue && !chat.IsPrivate.Value)
            {
                _logger.LogDebug("User has access to public group chat");
                return Result.Ok();
            }

            _logger.LogDebug("Checking user membership for chat access");

            var member = await _chatMemberRepository.GetByIdsAsync(chat.Id, user.Id, cancellationToken);

            if (member == null)
            {
                _logger.LogWarning("Chat access denied - user is not a member of the chat");
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CHAT_ACCESS_DENIED,
                    "You don't have access to this chat",
                    new
                    {
                        ChatId = chat.Id,
                        UserId = user.Id,
                        SuggestedAction = "Request access or join the chat"
                    }
                ));
            }

            _logger.LogDebug("User access validation successful");
            return Result.Ok();
        }

        private async Task<ChatVm> CreateChatViewModelAsync(Chat chat, User user, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Creating view model for chat type: {ChatType}", chat.Type);

            return chat.Type switch
            {
                ChatType.Personal => await CreatePersonalChatViewModelAsync(chat, cancellationToken),
                ChatType.OneToOne => await CreateOneToOneChatViewModelAsync(chat, user, cancellationToken),
                ChatType.Group => await CreateGroupChatViewModelAsync(chat, cancellationToken),
                _ => CreateDeletedChatViewModel(chat)
            };
        }

        private async Task<PersonalChatVm> CreatePersonalChatViewModelAsync(Chat chat, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Creating personal chat view model");

            return new PersonalChatVm
            {
                Id = chat.Id,
                Type = chat.Type,
                IsDeleted = chat.IsDeleted,
                CreatedAtUtc = chat.CreatedAtUtc,
                MessageCount = chat.Messages?.Count ?? 0
            };
        }

        private async Task<OneToOneChatVm> CreateOneToOneChatViewModelAsync(Chat chat, User currentUser, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Creating one-to-one chat view model");

            var otherMember = chat.Members?.FirstOrDefault(m => m.UserId != currentUser.Id);

            if (otherMember?.User == null)
            {
                _logger.LogError("OneToOne chat has invalid member structure - missing other user");
                return CreateDeletedOneToOneChatViewModel(chat);
            }

            if (otherMember.User.IsDeleted)
            {
                _logger.LogDebug("Other user in chat is deleted");
                return new OneToOneChatVm
                {
                    Id = chat.Id,
                    Type = chat.Type,
                    IsDeleted = chat.IsDeleted,
                    CreatedAtUtc = chat.CreatedAtUtc,
                    IsOtherUserDeleted = true,
                    Firstname = "Deleted User",
                    Lastname = null,
                    Bio = "This user account has been deleted",
                    Shortname = "deleted_user"
                };
            }

            var userMention = await _userMentionRepository
                .GetByUserIdAsync(otherMember.UserId, cancellationToken);

            return new OneToOneChatVm
            {
                Id = chat.Id,
                Type = chat.Type,
                IsDeleted = chat.IsDeleted,
                CreatedAtUtc = chat.CreatedAtUtc,
                IsOtherUserDeleted = false,
                Firstname = otherMember.User.Firstname,
                Lastname = otherMember.User.Lastname,
                Bio = otherMember.User.Bio ?? "No bio available",
                Shortname = userMention?.Shortname ?? "unknown_user"
            };
        }

        private async Task<GroupChatVm> CreateGroupChatViewModelAsync(Chat chat, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Creating group chat view model");

            // Get chat mention for shortname
            var chatMention = await _chatMentionRepository
                .GetByChatIdAsync(chat.Id, cancellationToken);

            return new GroupChatVm
            {
                Id = chat.Id,
                Type = chat.Type,
                IsDeleted = chat.IsDeleted,
                CreatedAtUtc = chat.CreatedAtUtc,
                Name = chat.Name ?? "Unnamed Group",
                Description = chat.Description ?? "No description available",
                Shortname = chatMention?.Shortname ?? "unknown_group",
                MemberCount = chat.Members?.Count ?? 0,
                IsPrivate = chat.IsPrivate ?? true
            };
        }

        private static DeletedChatVm CreateDeletedChatViewModel(Chat chat)
        {
            return new DeletedChatVm
            {
                Id = chat.Id,
                Type = chat.Type,
                CreatedAtUtc = chat.CreatedAtUtc
            };
        }

        private static OneToOneChatVm CreateDeletedOneToOneChatViewModel(Chat chat)
        {
            return new OneToOneChatVm
            {
                Id = chat.Id,
                Type = chat.Type,
                IsDeleted = true,
                CreatedAtUtc = chat.CreatedAtUtc,
                IsOtherUserDeleted = true,
                Firstname = "Unknown User",
                Lastname = null,
                Bio = "Chat data is corrupted",
                Shortname = "unknown_user"
            };
        }
    }
}
