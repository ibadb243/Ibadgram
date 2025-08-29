using Domain.Common;
using Domain.Common.Constants;
using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Repositories;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Chats.Commands.CreateGroup
{
    public class CreateGroupCommand : IRequest<Result<CreateGroupCommandResponse>>
    {
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsPrivate { get; set; } = false;
        public string? Shortname { get; set; }
    }

    public class CreateGroupCommandResponse
    {
        public Guid GroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPrivate { get; set; }
        public string? Shortname { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
    {
        public CreateGroupCommandValidator()
        {
            RuleFor(x => x.UserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("UserId is required");

            RuleFor(x => x.Name)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("Group name is required")
                .MinimumLength(ChatConstants.NameMinLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_SHORT)
                    .WithMessage($"Group name must be at least {ChatConstants.NameMinLength} characters long")
                .MaximumLength(ChatConstants.NameMaxLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_LONG)
                    .WithMessage($"Group name cannot exceed {ChatConstants.NameMaxLength} characters");

            RuleFor(x => x.Description)
                .Cascade(CascadeMode.Stop)
                .MaximumLength(ChatConstants.DescriptionLength)
                    .WithErrorCode(ErrorCodes.FIELD_TOO_LONG)
                    .WithMessage($"Description cannot exceed {ChatConstants.DescriptionLength} characters");

            When(x => !x.IsPrivate, () =>
            {
                RuleFor(x => x.Shortname)
                    .Cascade(CascadeMode.Stop)
                    .NotEmpty()
                        .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                        .WithMessage("Shortname is required for public groups")
                    .MinimumLength(ShortnameConstants.MinLength)
                        .WithErrorCode(ErrorCodes.FIELD_TOO_SHORT)
                        .WithMessage($"Shortname must be at least {ShortnameConstants.MinLength} characters long")
                    .MaximumLength(ShortnameConstants.MaxLength)
                        .WithErrorCode(ErrorCodes.FIELD_TOO_LONG)
                        .WithMessage($"Shortname cannot exceed {ShortnameConstants.MaxLength} characters");
            });
        }
    }

    public class CreateGroupCommandHandler : IRequestHandler<CreateGroupCommand, Result<CreateGroupCommandResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateGroupCommandHandler> _logger;

        public CreateGroupCommandHandler(
            IUnitOfWork unitOfWork,
            ILogger<CreateGroupCommandHandler> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Result<CreateGroupCommandResponse>> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting group creation process for user: {UserId}, group name: {GroupName}",
                request.UserId, request.Name);

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

                if (!request.IsPrivate)
                {
                    var shortnameValidationResult = await ValidateShortnameAvailability(request.Shortname!, cancellationToken);
                    if (shortnameValidationResult.IsFailed)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return shortnameValidationResult;
                    }
                }

                var group = await CreateGroupEntity(request, cancellationToken);
                var mention = await CreateMentionIfNeeded(request, group.Id, cancellationToken);
                await CreateCreatorMembership(group.Id, user.Id, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Group created successfully: {GroupId} by user: {UserId}",
                    group.Id, user.Id);

                return Result.Ok(new CreateGroupCommandResponse
                {
                    GroupId = group.Id,
                    Name = group.Name ?? string.Empty,
                    Description = group.Description ?? string.Empty,
                    IsPrivate = group.IsPrivate ?? false,
                    Shortname = mention?.Shortname,
                    CreatedAt = group.CreatedAtUtc
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Group creation failed for user: {UserId}", request.UserId);

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during group creation");
                }

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to create group due to system error"
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
                _logger.LogWarning("Group creation failed - user not found: {UserId}", userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_FOUND,
                    "User not found",
                    new { UserId = userId }
                ));
            }

            if (!user.IsVerified)
            {
                _logger.LogWarning("Group creation failed - user not verified: {UserId}", user.Id);
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
                _logger.LogWarning("Group creation failed - user is deleted: {UserId}", user.Id);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_DELETED,
                    "User account has been deleted",
                    new { UserId = user.Id }
                ));
            }

            _logger.LogDebug("User validation successful: {UserId}", user.Id);
            return Result.Ok(user);
        }

        private async Task<Result> ValidateShortnameAvailability(
            string shortname, 
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Checking shortname availability: {Shortname}", shortname);

            var existingMention = await _unitOfWork.MentionRepository.GetByShortnameAsync(shortname, cancellationToken);

            if (existingMention != null)
            {
                _logger.LogWarning("Group creation failed - shortname already taken: {Shortname}", shortname);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USERNAME_ALREADY_TAKEN,
                    "Shortname is already taken",
                    new
                    {
                        Shortname = shortname,
                        SuggestedAction = "Choose a different shortname"
                    }
                ));
            }

            _logger.LogDebug("Shortname is available: {Shortname}", shortname);
            return Result.Ok();
        }

        private async Task<Chat> CreateGroupEntity(
            CreateGroupCommand request, 
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Creating group entity with name: {GroupName}", request.Name);

            var group = new Chat
            {
                Id = Guid.NewGuid(),
                Type = ChatType.Group,
                Name = request.Name,
                Description = request.Description,
                IsPrivate = request.IsPrivate,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _unitOfWork.ChatRepository.AddAsync(group, cancellationToken);

            _logger.LogDebug("Group entity created with ID: {GroupId}", group.Id);
            return group;
        }

        private async Task<ChatMention?> CreateMentionIfNeeded(
            CreateGroupCommand request, 
            Guid groupId, 
            CancellationToken cancellationToken)
        {
            if (request.IsPrivate)
            {
                _logger.LogDebug("Skipping mention creation for private group: {GroupId}", groupId);
                return null;
            }

            _logger.LogDebug("Creating mention for public group: {GroupId} with shortname: {Shortname}",
                groupId, request.Shortname);

            var mention = new ChatMention
            {
                Id = Guid.NewGuid(),
                Shortname = request.Shortname!,
                ChatId = groupId
            };

            await _unitOfWork.MentionRepository.AddAsync(mention, cancellationToken);

            _logger.LogDebug("Mention created with ID: {MentionId}", mention.Id);
            return mention;
        }

        private async Task CreateCreatorMembership(
            Guid groupId,
            Guid userId, 
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Creating creator membership for group: {GroupId}, user: {UserId}",
                groupId, userId);

            var member = new ChatMember
            {
                ChatId = groupId,
                UserId = userId,
                Nickname = "Creator",
                Role = ChatRole.Creator,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _unitOfWork.ChatMemberRepository.AddAsync(member, cancellationToken);

            _logger.LogDebug("Creator membership created for user: {UserId} in group: {GroupId}",
                userId, groupId);
        }
    }
}
