using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Chats.Queries.GetGroupMembers
{
    public class MemberLookup
    {
        public Guid UserId { get; set; }
        public string Firstname { get; set; }
        public string? Lastname { get; set; }
        public ChatRole Role { get; set; }
        public string? Nickname { get; set; }
        public bool IsOnline { get; set; }
        public DateTime? LastSeen { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime JoinedAt { get; set; }
    }

    public class GetGroupMembersResponse
    {
        public List<MemberLookup> Members { get; set; } = new();
        public int TotalCount { get; set; }
        public int Offset { get; set; }
        public int Limit { get; set; }
        public bool HasNextPage => Offset + Limit < TotalCount;
        public bool HasPreviousPage => Offset > 0;
    }

    public class GetGroupMembersQuery : IRequest<Result<GetGroupMembersResponse>>
    {
        public Guid UserId { get; set; }
        public Guid ChatId { get; set; }
        public int Offset { get; set; } = 0;
        public int Limit { get; set; } = 50;
        public string? SearchTerm { get; set; }
        public ChatRole? RoleFilter { get; set; }
        public bool IncludeDeleted { get; set; } = false;
    }

    public class GetGroupMembersQueryValidator : AbstractValidator<GetGroupMembersQuery>
    {
        public GetGroupMembersQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithMessage("UserId is required");

            RuleFor(x => x.ChatId)
                .NotEmpty()
                    .WithMessage("ChatId is required");

            RuleFor(x => x.Offset)
                .GreaterThanOrEqualTo(0)
                    .WithMessage("Offset must be non-negative");

            RuleFor(x => x.Limit)
                .GreaterThanOrEqualTo(1)
                    .WithMessage("Limit must be between 1 and 200")
                .LessThanOrEqualTo(200)
                    .WithMessage("Limit must be between 1 and 200");

            RuleFor(x => x.SearchTerm)
                .MaximumLength(100)
                    .WithMessage("Search term cannot exceed 100 characters")
                .When(x => !string.IsNullOrEmpty(x.SearchTerm));
        }
    }

    public class GetGroupMembersQueryHandler : IRequestHandler<GetGroupMembersQuery, Result<GetGroupMembersResponse>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IChatMemberRepository _chatMemberRepository;
        private readonly ILogger<GetGroupMembersQueryHandler> _logger;

        public GetGroupMembersQueryHandler(
            IUserRepository userRepository,
            IChatRepository chatRepository,
            IChatMemberRepository chatMemberRepository,
            ILogger<GetGroupMembersQueryHandler> logger)
        {
            _userRepository = userRepository;
            _chatRepository = chatRepository;
            _chatMemberRepository = chatMemberRepository;
            _logger = logger;
        }

        public async Task<Result<GetGroupMembersResponse>> Handle(GetGroupMembersQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Getting group members for UserId: {UserId}, ChatId: {ChatId}, Offset: {Offset}, Limit: {Limit}",
                request.UserId, request.ChatId, request.Offset, request.Limit);

            var userValidationResult = await ValidateUserAsync(request.UserId, cancellationToken);
            if (userValidationResult.IsFailed)
                return userValidationResult.ToResult<GetGroupMembersResponse>();

            var user = userValidationResult.Value;

            var groupValidationResult = await ValidateGroupAsync(request.ChatId, cancellationToken);
            if (groupValidationResult.IsFailed)
                return groupValidationResult.ToResult<GetGroupMembersResponse>();

            var group = groupValidationResult.Value;

            var membershipValidationResult = await ValidateMembershipAsync(group.Id, user.Id, cancellationToken);
            if (membershipValidationResult.IsFailed)
                return membershipValidationResult.ToResult<GetGroupMembersResponse>();

            var currentUserMember = membershipValidationResult.Value;

            var membersResult = await GetMembersAsync(request, currentUserMember, cancellationToken);
            if (membersResult.IsFailed)
                return membersResult;

            _logger.LogInformation(
                "Successfully retrieved {Count} members for group {ChatId}",
                membersResult.Value.Members.Count, request.ChatId);

            return membersResult;
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

        private async Task<Result<Chat>> ValidateGroupAsync(Guid chatId, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Validating group {ChatId}", chatId);

            var group = await _chatRepository.GetByIdAsync(chatId, cancellationToken);

            if (group == null)
            {
                _logger.LogWarning("Group {ChatId} not found", chatId);
                return Result.Fail("Group not found");
            }

            if (group.Type != ChatType.Group)
            {
                _logger.LogWarning("Chat {ChatId} is not a group (Type: {Type})", chatId, group.Type);
                return Result.Fail("Chat is not a group");
            }

            if (group.IsDeleted)
            {
                _logger.LogWarning("Group {ChatId} is deleted", chatId);
                return Result.Fail("Group is deleted");
            }

            return Result.Ok(group);
        }

        private async Task<Result<ChatMember>> ValidateMembershipAsync(
            Guid chatId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Validating membership for user {UserId} in chat {ChatId}", userId, chatId);

            var member = await _chatMemberRepository.GetByIdsAsync(chatId, userId, cancellationToken);

            if (member == null)
            {
                _logger.LogWarning("User {UserId} is not a member of group {ChatId}", userId, chatId);
                return Result.Fail("You are not a member of this group");
            }

            // If add IsDelete/IsBanned property for member
            //if (member.IsDeleted)
            //{
            //    _logger.LogWarning("User {UserId} membership in group {ChatId} is deleted", userId, chatId);
            //    return Result.Fail("Your membership has been revoked");
            //}

            return Result.Ok(member);
        }

        private async Task<Result<GetGroupMembersResponse>> GetMembersAsync(
            GetGroupMembersQuery request,
            ChatMember currentUserMember,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving members for chat {ChatId}", request.ChatId);

            try
            {
                // Получаем всех участников
                var allMembers = await _chatMemberRepository.GetByChatIdAsync(request.ChatId, cancellationToken);

                // Применяем фильтры
                var filteredMembers = ApplyFilters(allMembers, request, currentUserMember);

                // Применяем пагинацию
                var paginatedMembers = filteredMembers
                    .Skip(request.Offset)
                    .Take(request.Limit)
                    .ToList();

                // Маппим в ViewModel
                var memberLookups = paginatedMembers
                    .Select(member => MapToMemberLookup(member))
                    .ToList();

                var response = new GetGroupMembersResponse
                {
                    Members = memberLookups,
                    TotalCount = filteredMembers.Count(),
                    Offset = request.Offset,
                    Limit = request.Limit
                };

                return Result.Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while retrieving members for chat {ChatId}", request.ChatId);
                return Result.Fail("An error occurred while retrieving group members");
            }
        }

        private static IEnumerable<ChatMember> ApplyFilters(
            IEnumerable<ChatMember> members,
            GetGroupMembersQuery request,
            ChatMember currentUserMember)
        {
            var filteredMembers = members.AsEnumerable();

            if (!request.IncludeDeleted)
            {
                filteredMembers = filteredMembers.Where(m => !m.User.IsDeleted /*&& !m.IsDeleted*/);
            }

            if (request.RoleFilter.HasValue)
            {
                filteredMembers = filteredMembers.Where(m => (m.Role ?? ChatRole.Member) == request.RoleFilter.Value);
            }

            if (!string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                var searchTerm = request.SearchTerm.ToLowerInvariant();
                filteredMembers = filteredMembers.Where(m =>
                    m.User.Firstname.ToLowerInvariant().Contains(searchTerm) ||
                    (m.User.Lastname?.ToLowerInvariant().Contains(searchTerm) ?? false) ||
                    (m.Nickname?.ToLowerInvariant().Contains(searchTerm) ?? false));
            }

            filteredMembers = filteredMembers
                .OrderByDescending(m => m.Role == ChatRole.Admin || m.Role == ChatRole.Creator)
                .ThenBy(m => m.CreatedAtUtc);

            return filteredMembers;
        }

        private static MemberLookup MapToMemberLookup(ChatMember member)
        {
            return new MemberLookup
            {
                UserId = member.UserId,
                Firstname = member.User.Firstname,
                Lastname = member.User.Lastname,
                Role = member.Role ?? ChatRole.Member,
                Nickname = member.Nickname,
                IsOnline = member.User.Status == UserStatus.Online,
                LastSeen = member.User.LastSeenAt,
                AvatarUrl = member.User.Avatar,
                IsDeleted = member.User.IsDeleted /*|| member.IsDeleted*/,
                JoinedAt = member.CreatedAtUtc
            };
        }
    }
}
