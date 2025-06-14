using Application.Interfaces.Repositories;
using Domain.Common;
using Domain.Common.Constants;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Application.CQRS.Chats.Commands.UpdateGroup
{
    public class GroupVm
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsPrivate { get; set; }
        public string? Shortname { get; set; }
    }

    public class UpdateGroupCommand : IRequest<GroupVm>
    {
        public Guid UserId { get; set; }
        public Guid GroupId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsPrivate { get; set; }
        public string? Shortname { get; set; }
    }

    public class UpdateGroupCommandValidator : AbstractValidator<UpdateGroupCommand>
    {
        public UpdateGroupCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty();

            RuleFor(x => x.GroupId)
                .NotEmpty();

            RuleFor(x => x.Name)
                .NotEmpty()
                .MinimumLength(ChatConstants.NameMinLength)
                .MaximumLength(ChatConstants.NameMaxLength);

            RuleFor(x => x.Description)
                .MaximumLength(ChatConstants.DescriptionLength);

            When(x => !x.IsPrivate, () =>
            {
                RuleFor(x => x.Shortname)
                    .NotEmpty()
                    .MinimumLength(ShortnameConstants.MinLength)
                    .MaximumLength(ShortnameConstants.MaxLength);
            });
        }
    }

    public class UpdateGroupCommandHandler : IRequestHandler<UpdateGroupCommand, GroupVm>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateGroupCommandHandler(
            IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<GroupVm> Handle(UpdateGroupCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);
                if (user == null) throw new Exception("User not found");

                var group = await _unitOfWork.ChatRepository.GetByIdAsync(request.GroupId, cancellationToken);
                if (group == null) throw new Exception("Group not found");

                var member = await _unitOfWork.ChatMemberRepository.GetByIdsAsync(group.Id, user.Id, cancellationToken);
                if (member == null) throw new Exception("You are member of group");

                if (member.Role != ChatRole.Creator) throw new Exception("You are not creator of group");

                group.Name = request.Name;
                group.Description = request.Description;

                if (!group.IsPrivate.Value && !request.IsPrivate && group.Mention?.Shortname != request.Shortname)
                {
                    var mention = await _unitOfWork.MentionRepository.GetByShortnameAsync(request.Shortname, cancellationToken);
                    if (mention != null) throw new Exception("Shortname had taken");

                    mention = group.Mention;
                    await _unitOfWork.MentionRepository.DeleteAsync(mention!, cancellationToken);

                    mention = new ChatMention
                    {
                        Shortname = request.Shortname,
                        ChatId = group.Id,
                        Chat = group,
                    };

                    await _unitOfWork.MentionRepository.AddAsync(mention, cancellationToken);
                    group.Mention = (ChatMention)mention;
                }
                else if (!group.IsPrivate.Value && request.IsPrivate)
                {
                    await _unitOfWork.MentionRepository.DeleteAsync(group.Mention!, cancellationToken);
                    group.Mention = null;
                }
                else if (group.IsPrivate.Value && !request.IsPrivate)
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
                    group.Mention = (ChatMention)mention;
                }

                await _unitOfWork.ChatRepository.UpdateAsync(group, cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new GroupVm
                {
                    Name = group.Name,
                    Description = group.Description,
                    IsPrivate = group.IsPrivate.Value,
                    Shortname = group.IsPrivate.Value ? null : group.Mention!.Shortname,
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
