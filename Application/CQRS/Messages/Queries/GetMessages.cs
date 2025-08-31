
using Domain.Common;
using Domain.Entities;
using Domain.Errors;
using Domain.Repositories;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Messages.Queries.GetMessages
{
    public class GetMessageListQuery : IRequest<Result<GetMessageListQueryResponse>>
    {
        public Guid UserId { get; set; }
        public Guid ChatId { get; set; }
        public int Limit { get; set; } = 20;
        public int Offset { get; set; } = 0;
    }

    public class GetMessageListQueryResponse
    {
        public List<MessageDto> Messages { get; set; } = new();
        public PaginationInfo Pagination { get; set; } = new();
        public ChatInfo ChatInfo { get; set; } = new();
    }

    public class MessageDto
    {
        public Guid UserId { get; set; }
        public Guid ChatId { get; set; }
        public long MessageId { get; set; }
        public string Fullname { get; set; } = string.Empty;
        public string? Nickname { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsEdited { get; set; } = false;
        public DateTime Timestamp { get; set; } 
    }

    public class PaginationInfo
    {
        public int Offset { get; set; }
        public int Limit { get; set; }
        public int TotalCount { get; set; }
        public bool HasNextPage { get; set; }
        public long? NextCursor { get; set; } // For cursor-based pagination
    }

    public class ChatInfo
    {
        public Guid ChatId { get; set; }
        public string? ChatName { get; set; }
        public bool IsPrivate { get; set; }
        public string? UserRole { get; set; }
    }

    public class GetMessageListQueryValidation : AbstractValidator<GetMessageListQuery>
    {
        public GetMessageListQueryValidation()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                .WithMessage("UserId is required");

            RuleFor(x => x.ChatId)
                .NotEmpty()
                .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                .WithMessage("ChatId is required");

            RuleFor(x => x.Limit)
                .InclusiveBetween(1, 100)
                .WithErrorCode(ErrorCodes.INVALID_RANGE)
                .WithMessage("Limit must be between 1 and 100");

            RuleFor(x => x.Offset)
                .GreaterThanOrEqualTo(0)
                .WithErrorCode(ErrorCodes.INVALID_RANGE)
                .WithMessage("Offset must be greater than or equal to 0");
        }
    }

    public class GetMessageListQueryHandler : IRequestHandler<GetMessageListQuery, Result<GetMessageListQueryResponse>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IChatMemberRepository _chatMemberRepository;
        private readonly IMessageRepository _messageRepository;
        private readonly ILogger<GetMessageListQueryHandler> _logger;

        public GetMessageListQueryHandler(
            IUserRepository userRepository,
            IChatRepository chatRepository,
            IChatMemberRepository chatMemberRepository,
            IMessageRepository messageRepository,
            ILogger<GetMessageListQueryHandler> logger)
        {
            _userRepository = userRepository;
            _chatRepository = chatRepository;
            _chatMemberRepository = chatMemberRepository;
            _messageRepository = messageRepository;
            _logger = logger;
        }

        public async Task<Result<GetMessageListQueryResponse>> Handle(GetMessageListQuery request, CancellationToken cancellationToken)
        {
            using var activity = _logger.BeginScope("GetMessageList {UserId} {ChatId}", request.UserId, request.ChatId);

            _logger.LogInformation("Starting message retrieval for user: {UserId} from chat: {ChatId}",
                request.UserId, request.ChatId);

            try
            {
                // Validate user access
                var accessResult = await ValidateUserAccessAsync(request.UserId, request.ChatId, cancellationToken);
                if (accessResult.IsFailed)
                    return accessResult.ToResult();

                var (user, chat, chatMember) = accessResult.Value;

                // Get messages with optimized query
                var messagesResult = await GetMessagesAsync(request, chat.Id, cancellationToken);
                if (messagesResult.IsFailed)
                    return messagesResult.ToResult();

                var (messages, totalCount, hasNextPage) = messagesResult.Value;

                var response = new GetMessageListQueryResponse
                {
                    Messages = messages.Select(m => MapToMessageDto(m, chatMember)).ToList(),
                    Pagination = new PaginationInfo
                    {
                        Offset = request.Offset,
                        Limit = request.Limit,
                        TotalCount = totalCount,
                        HasNextPage = hasNextPage,
                        NextCursor = messages.LastOrDefault()?.Id
                    },
                    ChatInfo = new ChatInfo
                    {
                        ChatId = chat.Id,
                        ChatName = chat.Name,
                        IsPrivate = chat.IsPrivate ?? false,
                        UserRole = chatMember?.Role?.ToString()
                    }
                };

                _logger.LogInformation("Successfully retrieved {MessageCount} messages for user: {UserId}",
                    messages.Count, request.UserId);

                return Result.Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve messages for user: {UserId} from chat: {ChatId}",
                    request.UserId, request.ChatId);

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to retrieve messages due to system error"
                ));
            }
        }


        private async Task<Result<(User user, Chat chat, ChatMember? chatMember)>> ValidateUserAccessAsync(
            Guid userId, Guid chatId, CancellationToken cancellationToken)
        {
            // Get user
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_FOUND,
                    "User not found",
                    new { UserId = userId }
                ));
            }

            if (user.IsDeleted)
            {
                _logger.LogWarning("User is deleted: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_DELETED,
                    "User account has been deleted"
                ));
            }

            if (!user.IsVerified)
            {
                _logger.LogWarning("User is not verified: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_VERIFIED,
                    "User account is not verified"
                ));
            }

            // Get chat
            var chat = await _chatRepository.GetByIdAsync(chatId, cancellationToken);
            if (chat == null)
            {
                _logger.LogWarning("Chat not found: {ChatId}", chatId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CHAT_NOT_FOUND,
                    "Chat not found",
                    new { ChatId = chatId }
                ));
            }

            if (chat.IsDeleted)
            {
                _logger.LogWarning("Chat is deleted: {ChatId}", chatId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CHAT_DELETED,
                    "Chat has been deleted"
                ));
            }

            // Check membership for private chats
            ChatMember? chatMember = null;
            if (chat.IsPrivate == true)
            {
                chatMember = await _chatMemberRepository.GetByIdsAsync(chatId, userId, cancellationToken);
                if (chatMember == null)
                {
                    _logger.LogWarning("User {UserId} is not a member of private chat {ChatId}", userId, chatId);
                    return Result.Fail(new BusinessLogicError(
                        ErrorCodes.CHAT_ACCESS_DENIED,
                        "You are not a member of this chat"
                    ));
                }
            }
            else
            {
                // For public chats, still try to get membership for role/nickname info
                chatMember = await _chatMemberRepository.GetByIdsAsync(chatId, userId, cancellationToken);
            }

            return Result.Ok((user, chat, chatMember));
        }

        private async Task<Result<(List<Message> messages, int totalCount, bool hasNextPage)>> GetMessagesAsync(
            GetMessageListQuery request, Guid chatId, CancellationToken cancellationToken)
        {
            try
            {
                // Use repository method for optimized query with proper includes
                var messages = (await _messageRepository.GetChatMessagesAsync(
                    chatId,
                    request.Limit + 1, // Get one extra to check if there are more
                    request.Offset,
                    cancellationToken))
                        .ToList();

                var hasNextPage = messages.Count() > request.Limit;
                if (hasNextPage)
                {
                    messages = messages
                        .Take(request.Limit)
                        .ToList();
                }

                // Get total count for pagination info (consider caching this for better performance)
                var totalCount = await _messageRepository.GetChatMessageCountAsync(chatId, cancellationToken);

                return Result.Ok((messages, totalCount, hasNextPage));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve messages from database for chat: {ChatId}", chatId);
                throw;
            }
        }

        private static MessageDto MapToMessageDto(Message message, ChatMember? chatMember)
        {
            return new MessageDto
            {
                MessageId = message.Id,
                UserId = message.UserId,
                Fullname = $"{message.User.Firstname} {message.User.Lastname}".Trim(),
                Nickname = chatMember?.Nickname,
                Text = message.Text,
                IsEdited = message.UpdatedAtUtc.HasValue,
                Timestamp = message.CreatedAtUtc,
            };
        }
    }
}
