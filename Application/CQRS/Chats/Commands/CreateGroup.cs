using Application.Interfaces.Repositories;
using Domain.Common.Constants;
using Domain.Entities;
using Domain.Enums;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Chats.Commands.CreateGroup
{
    public class CreateGroupCommand : IRequest<Result<Guid>>
    {
        public Guid UserId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsPrivate { get; set; } = false;
        public string? Shortname { get; set; }
    }

    public class CreateGroupCommandValidator : AbstractValidator<CreateGroupCommand>
    {
        public CreateGroupCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                    .WithMessage("UserId is required");

            RuleFor(x => x.Name)
                .NotEmpty()
                    .WithMessage("Name is required")
                .MinimumLength(ChatConstants.NameMinLength)
                    .WithMessage($"Name's length should have minimum {ChatConstants.NameMinLength} characters")
                .MaximumLength(ChatConstants.NameMaxLength)
                    .WithMessage($"Name's length cann't have characters greater than {ChatConstants.NameMaxLength}");

            RuleFor(x => x.Description)
                .MaximumLength(ChatConstants.DescriptionLength)
                    .WithMessage($"Description's length cann't have characters greater than {UserConstants.BioLength}");

            When(x => !x.IsPrivate, () =>
            {
                RuleFor(x => x.Shortname)
                    .NotEmpty()
                        .WithMessage("Shortname is required")
                    .MinimumLength(ShortnameConstants.MinLength)
                        .WithMessage($"Shortname's length should have minimum {ShortnameConstants.MinLength} characters")
                    .MaximumLength(ShortnameConstants.MaxLength)
                        .WithMessage($"Shortname's length cann't have characters greater than {ShortnameConstants.MaxLength}");
            });
        }
    }

    public class CreateGroupCommandHandler : IRequestHandler<CreateGroupCommand, Result<Guid>>
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

        public async Task<Result<Guid>> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting create group proccess");

            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                _logger.LogDebug("Retrieving user by id {UserId} from database", request.UserId);
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);

                if (user == null)
                {
                    _logger.LogWarning("Create group failed - user not found");
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User not found");
                }

                if (!user.IsVerified)
                {
                    _logger.LogWarning("Create group failed - user with id {UserId} is deleted", request.UserId);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User isn't verified");
                }

                if (user.IsDeleted)
                {
                    _logger.LogWarning("Create group failed - user with id {UserId} is deleted", request.UserId);
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return Result.Fail("User is deleted");
                }

                _logger.LogInformation("User validated successfully");

                Chat group = new Chat
                {
                    Id = Guid.NewGuid(),
                    Type = ChatType.Group,
                    Name = request.Name,
                    Description = request.Description,
                    IsPrivate = request.IsPrivate,
                };

                _logger.LogDebug("Adding group with id {ChatId} to database", group.Id);
                await _unitOfWork.ChatRepository.AddAsync(group, cancellationToken);

                if (!request.IsPrivate)
                {
                    _logger.LogInformation("Binding mention");

                    _logger.LogDebug("Retrieving mention by shortname {Shortname} from database", request.Shortname);
                    var mention = await _unitOfWork.MentionRepository.GetByShortnameAsync(request.Shortname, cancellationToken);

                    if (mention != null)
                    {
                        _logger.LogWarning("Binding mention failed - mention has already been taken");
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return Result.Fail("Shortname has already been taken");
                    }

                    mention = new ChatMention
                    {
                        Id= Guid.NewGuid(),
                        Shortname = request.Shortname,
                        ChatId = group.Id,
                        Chat = group,
                    };

                    _logger.LogDebug("Adding mention with id {MentionId} to database", mention.Id);
                    await _unitOfWork.MentionRepository.AddAsync(mention, cancellationToken);
                }

                var member = new ChatMember
                {
                    ChatId = group.Id,
                    UserId = user.Id,
                    Nickname = "Creator",
                    Role = ChatRole.Creator,
                    Chat = group,
                    User = user,
                };

                _logger.LogDebug("Adding creator member to database");
                await _unitOfWork.ChatMemberRepository.AddAsync(member, cancellationToken);

                _logger.LogDebug("Saving changes to databse");
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogDebug("Committing transaction");
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return group.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Creating group failed");

                try
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    _logger.LogDebug("Transaction rolled back successfully");
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogError(rollbackEx, "Failed to rollback transaction during create group");
                }

                throw;
            }
        }
    }
}
