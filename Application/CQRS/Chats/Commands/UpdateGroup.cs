using Application.Interfaces.Repositories;
using Domain.Common;
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
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.GroupId).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MinimumLength(1).MaximumLength(256);
            RuleFor(x => x.Description).MaximumLength(2048);
            When(x => x.IsPrivate, () =>
            {
                RuleFor(x => x.Shortname).NotEmpty().MinimumLength(4).MaximumLength(64);
            });
        }
    }

    public class UpdateGroupCommandHandler : IRequestHandler<UpdateGroupCommand, GroupVm>
    {
        private readonly IUserRepository _userRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IMentionRepository _mentionRepository;
        private readonly IChatMemberRepository _chatMemberRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateGroupCommandHandler(
            IUserRepository userRepository,
            IChatRepository chatRepository,
            IMentionRepository mentionRepository,
            IChatMemberRepository chatMemberRepository,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _chatRepository = chatRepository;
            _mentionRepository = mentionRepository;
            _chatMemberRepository = chatMemberRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<GroupVm> Handle(UpdateGroupCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null) throw new Exception("User not found");

            var group = await _chatRepository.GetByIdAsync(request.GroupId, cancellationToken);
            if (group == null) throw new Exception("Group not found");

            var member = await _chatMemberRepository.GetByIdsAsync(group.Id, user.Id, cancellationToken);
            if (member == null) throw new Exception("You are member of group");

            if (member.Role != ChatRole.Creator) throw new Exception("You are not creator of group");

            group.Name = request.Name;
            group.Description = request.Description;

            if (!group.IsPrivate.Value && !request.IsPrivate && group.Mention?.Shortname != request.Shortname)
            {
                var mention = await _mentionRepository.GetByShortnameAsync(request.Shortname, cancellationToken);
                if (mention != null) throw new Exception("Shortname had taken");

                mention = group.Mention;
                await _mentionRepository.DeleteAsync(mention!, cancellationToken);

                mention = new ChatMention
                {
                    Shortname = request.Shortname,
                    ChatId = group.Id,
                    Chat = group,
                };

                await _mentionRepository.AddAsync(mention, cancellationToken);
                group.Mention = (ChatMention)mention;
            } else if (!group.IsPrivate.Value && request.IsPrivate)
            {
                await _mentionRepository.DeleteAsync(group.Mention!, cancellationToken);
                group.Mention = null;
            } else if (group.IsPrivate.Value && !request.IsPrivate)
            {
                var mention = await _mentionRepository.GetByShortnameAsync(request.Shortname, cancellationToken);
                if (mention != null) throw new Exception("Shortname had taken");

                mention = new ChatMention
                {
                    Shortname = request.Shortname,
                    ChatId = group.Id,
                    Chat = group,
                };

                await _mentionRepository.AddAsync(mention, cancellationToken);
                group.Mention = (ChatMention)mention;
            }

            await _chatRepository.UpdateAsync(group, cancellationToken);

            return new GroupVm {
                Name = group.Name,
                Description = group.Description,
                IsPrivate = group.IsPrivate.Value,
                Shortname = group.IsPrivate.Value ? null : group.Mention!.Shortname,
            };
        }
    }
}
