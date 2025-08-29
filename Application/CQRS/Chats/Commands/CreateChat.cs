using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Repositories;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

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
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("FirstUserId is required");

            RuleFor(x => x.SecondUserId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("SecondUserId is required");

            RuleFor(x => x)
                .Cascade(CascadeMode.Stop)
                .Must(BeNotEqual)
                    .WithErrorCode(ErrorCodes.INVALID_FORMAT)
                    .WithMessage("FirstUserId and SecondUserId must be different");
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
            _logger.LogInformation("Starting chat creation process between users: {FirstUserId} and {SecondUserId}",
                request.FirstUserId, request.SecondUserId);

            await _unitOfWork.BeginTransactionAsync(cancellationToken: cancellationToken);

            try
            {
                var userValidationResult = await ValidateUsersAsync(request, cancellationToken);
                if (userValidationResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return userValidationResult.ToResult();
                }

                var (firstUser, secondUser) = userValidationResult.Value;

                var existingChatResult = await CheckExistingChatAsync(firstUser.Id, secondUser.Id, cancellationToken);
                if (existingChatResult.IsFailed)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return existingChatResult;
                }

                var chat = await CreateChatWithMembersAsync(firstUser, secondUser, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                _logger.LogInformation("Chat created successfully with ID: {ChatId}", chat.Id);
                return Result.Ok(chat.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Chat creation failed between users: {FirstUserId} and {SecondUserId}",
                    request.FirstUserId, request.SecondUserId);

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during chat creation");
                }

                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.DATABASE_ERROR,
                    "Unable to create chat due to system error"
                ));
            }
        }

        private async Task<Result<(User FirstUser, User SecondUser)>> ValidateUsersAsync(
            CreateChatCommand request,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Validating users for chat creation");

            var firstUserResult = await ValidateUserAsync(request.FirstUserId, "first", cancellationToken);
            if (firstUserResult.IsFailed)
                return Result.Fail<(User, User)>(firstUserResult.Errors);

            var secondUserResult = await ValidateUserAsync(request.SecondUserId, "second", cancellationToken);
            if (secondUserResult.IsFailed)
                return Result.Fail<(User, User)>(secondUserResult.Errors);

            return Result.Ok((firstUserResult.Value, secondUserResult.Value));
        }

        private async Task<Result<User>> ValidateUserAsync(
            Guid userId,
            string userType,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Retrieving {UserType} user by ID: {UserId}", userType, userId);

            var user = await _unitOfWork.UserRepository.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("Chat creation failed - {UserType} user not found: {UserId}", userType, userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_FOUND,
                    $"{char.ToUpper(userType[0])}{userType[1..]} user not found",
                    new { UserId = userId }
                ));
            }

            if (user.IsDeleted)
            {
                _logger.LogWarning("Chat creation failed - {UserType} user is deleted: {UserId}", userType, userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_DELETED,
                    $"{char.ToUpper(userType[0])}{userType[1..]} user account has been deleted"
                ));
            }

            if (!user.IsVerified)
            {
                _logger.LogWarning("Chat creation failed - {UserType} user is not verified: {UserId}", userType, userId);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.USER_NOT_VERIFIED,
                    $"{char.ToUpper(userType[0])}{userType[1..]} user account is not verified",
                    new
                    {
                        UserId = userId,
                        SuggestedAction = "Await complete account verification"
                    }
                ));
            }

            _logger.LogDebug("{UserType} user validation successful: {UserId}", userType, userId);
            return Result.Ok(user);
        }

        private async Task<Result> CheckExistingChatAsync(
            Guid firstUserId,
            Guid secondUserId,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Checking for existing chat between users");

            var existingChat = await _unitOfWork.ChatRepository
                .FindOneToOneChatAsync(firstUserId, secondUserId, cancellationToken);

            if (existingChat != null)
            {
                _logger.LogWarning("Chat already exists between users. ChatId: {ChatId}", existingChat.Id);
                return Result.Fail(new BusinessLogicError(
                    ErrorCodes.CHAT_ALREADY_EXISTS,
                    "Chat already exists between these users"
                ));
            }

            _logger.LogDebug("No existing chat found between users");
            return Result.Ok();
        }

        private async Task<Chat> CreateChatWithMembersAsync(
            User firstUser,
            User secondUser,
            CancellationToken cancellationToken)
        {
            _logger.LogDebug("Creating new chat and members");

            var chat = new Chat
            {
                Id = Guid.NewGuid(),
                Type = ChatType.OneToOne,
                CreatedAtUtc = DateTime.UtcNow
            };

            await _unitOfWork.ChatRepository.AddAsync(chat, cancellationToken);

            var members = new[]
            {
                CreateChatMember(chat.Id, firstUser.Id, chat, firstUser),
                CreateChatMember(chat.Id, secondUser.Id, chat, secondUser)
            };

            foreach (var member in members)
            {
                await _unitOfWork.ChatMemberRepository.AddAsync(member, cancellationToken);
            }

            _logger.LogDebug("Chat and members created with ChatId: {ChatId}", chat.Id);
            return chat;
        }

        private static ChatMember CreateChatMember(Guid chatId, Guid userId, Chat chat, User user)
        {
            return new ChatMember
            {
                ChatId = chatId,
                UserId = userId,
                Chat = chat,
                User = user
            };
        }
    }
}
