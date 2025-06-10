using Application.Interfaces.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Chats.Commands.CreateGroup
{
    public class GroupVm
    {
        public Guid GroupId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string? Shortname { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class CreateGroupCommand : IRequest<GroupVm>
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
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MinimumLength(1).MaximumLength(256);
            RuleFor(x => x.Description).MaximumLength(2048);
            When(x => x.IsPrivate, () =>
            {
                RuleFor(x => x.Shortname).NotEmpty().MinimumLength(4).MaximumLength(64);
            });
        }
    }

    public class CreateGroupCommandHandler : IRequestHandler<CreateGroupCommand, GroupVm>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateGroupCommandHandler(
            IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<GroupVm> Handle(CreateGroupCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);
                if (user == null) throw new Exception("User not found");

                Chat group = new Chat
                {
                    Type = ChatType.Group,
                    Name = request.Name,
                    Description = request.Description,
                    IsPrivate = request.IsPrivate,
                };

                await _unitOfWork.ChatRepository.AddAsync(group, cancellationToken);

                if (!request.IsPrivate)
                {
                    var mention = await _unitOfWork.MentionRepository.GetByShortnameAsync(request.Shortname, cancellationToken);
                    if (mention != null) throw new Exception("Shortname had taken");

                    mention = new ChatMention
                    {
                        Shortname = request.Shortname,
                        ChatId = group.Id,
                        Chat = group,
                    };

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

                await _unitOfWork.ChatMemberRepository.AddAsync(member, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new GroupVm
                {
                    GroupId = group.Id,
                    Name = request.Name,
                    Description = request.Description,
                    Shortname = request.IsPrivate ? null : request.Shortname,
                    CreatedAtUtc = group.CreatedAtUtc,
                };
            }
            catch 
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
