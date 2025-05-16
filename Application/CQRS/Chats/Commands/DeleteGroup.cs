using Application.Interfaces.Repositories;
using Domain.Enums;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Chats.Commands.DeleteGroup
{
    public class DeleteGroupCommand : IRequest<Unit>
    {
        public Guid UserId { get; set; }
        public Guid GroupId { get; set; }
    }

    public class DeleteGroupCommandValidator : AbstractValidator<DeleteGroupCommand>
    {
        public DeleteGroupCommandValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.GroupId).NotEmpty();
        }
    }

    public class DeleteGroupCommandHandler : IRequestHandler<DeleteGroupCommand, Unit>
    {
        private readonly IUserRepository _userRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IChatMemberRepository _chatMemberRepository;
        private readonly IMentionRepository _mentionRepository;
        private readonly IUnitOfWork _unitOfWork;

        public DeleteGroupCommandHandler(
            IUserRepository userRepository,
            IChatRepository chatRepository,
            IChatMemberRepository chatMemberRepository,
            IMentionRepository mentionRepository,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _chatRepository = chatRepository;
            _chatMemberRepository = chatMemberRepository;
            _mentionRepository = mentionRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null) throw new Exception("User not found");

            var group = await _chatRepository.GetByIdAsync(request.GroupId, cancellationToken);
            if (group == null) throw new Exception("Group not found");

            var member = await _chatMemberRepository.GetByIdsAsync(group.Id, user.Id, cancellationToken);
            if (member == null) throw new Exception("You are member of group");

            if (member.Role != ChatRole.Creator) throw new Exception("You are not creator of group");

            await _chatRepository.DeleteAsync(group, cancellationToken);

            if (!group.IsPrivate.Value) await _mentionRepository.DeleteAsync(group.Mention);

            await _unitOfWork.SaveAsync(cancellationToken);

            return Unit.Value;
        }
    }
}
