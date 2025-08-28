using Application.Interfaces.Repositories;
using Domain.Common;
using Domain.Entities;
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

namespace Application.CQRS.Memberships.Queries.UserMembership
{
    public class GetUserMembershipQuery : IRequest<Result<GetUserMembershipQueryResponse>>
    {
        public Guid UserId { get; set; }
    }

    public class GetUserMembershipQueryResponse
    {
        public List<ChatLoopUp> Chats { get; set; }
    }

    public class ChatLoopUp
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
    }

    public class GetUserMembershipQueryValidator : AbstractValidator<GetUserMembershipQuery>
    {
        public GetUserMembershipQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("UserId is required");
        }
    }

    public class GetUserMembershipQueryHandler : IRequestHandler<GetUserMembershipQuery, Result<GetUserMembershipQueryResponse>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IChatMemberRepository _chatMemberRepository;
        private readonly ILogger<GetUserMembershipQueryHandler> _logger;

        public GetUserMembershipQueryHandler(
            IUserRepository userRepository,
            IChatMemberRepository chatMemberRepository,
            ILogger<GetUserMembershipQueryHandler> logger)
        {
            _userRepository = userRepository;
            _chatMemberRepository = chatMemberRepository;
            _logger = logger;
        }

        public async Task<Result<GetUserMembershipQueryResponse>> Handle(GetUserMembershipQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting user membership retrieval process for user: {UserId}", request.UserId);

            try
            {
                var userResult = await GetAndValidateUserAsync(request.UserId, cancellationToken);
                if (userResult.IsFailed)
                {
                    return userResult.ToResult();
                }

                var user = userResult.Value;

                var chatMemberships = await GetUserChatMembershipsAsync(user.Id, cancellationToken);

                _logger.LogInformation("Successfully retrieved {ChatCount} chat memberships for user: {UserId}",
                    chatMemberships.Count, user.Id);

                return Result.Ok(new GetUserMembershipQueryResponse
                {
                    Chats = chatMemberships
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve user memberships for user: {UserId}", request.UserId);

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to retrieve user memberships due to system error"
                ));
            }
        }

        private async Task<Result<User>> GetAndValidateUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Validating user for membership retrieval: {UserId}", userId);

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("User membership retrieval failed - user not found: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_FOUND,
                    "User not found",
                    new { UserId = userId }
                ));
            }

            if (user.IsDeleted)
            {
                _logger.LogWarning("User membership retrieval failed - user is deleted: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_DELETED,
                    "User account has been deleted"
                ));
            }

            if (!user.IsVerified)
            {
                _logger.LogWarning("User membership retrieval failed - user is not verified: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_VERIFIED,
                    "User account is not verified",
                    new
                    {
                        UserId = userId,
                        SuggestedAction = "Complete account verification first"
                    }
                ));
            }

            _logger.LogDebug("User validation successful for membership retrieval: {UserId}", userId);
            return Result.Ok(user);
        }

        private async Task<List<ChatLoopUp>> GetUserChatMembershipsAsync(Guid userId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving chat memberships for user: {UserId}", userId);

            var chatMembers = await _chatMemberRepository.GetByUserIdAsync(userId, cancellationToken);

            var chatLookups = chatMembers
                .Where(cm => cm.Chat != null && !cm.Chat.IsDeleted)
                .Select(cm => new ChatLoopUp
                {
                    Id = cm.Chat.Id,
                    Name = GetChatDisplayName(cm.Chat)
                })
                .OrderBy(chat => chat.Name)
                .ToList();

            _logger.LogDebug("Retrieved {ChatCount} active chat memberships for user: {UserId}",
                chatLookups.Count, userId);

            return chatLookups;
        }

        private static string GetChatDisplayName(Chat chat)
        {
            // Для one-to-one чатов можно использовать имя собеседника
            // Для групповых чатов - название группы
            return chat.Name ?? $"Chat {chat.Id:N}";
        }
    }
}
