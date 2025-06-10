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
        private readonly IUnitOfWork _unitOfWork;

        public DeleteGroupCommandHandler(
            IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(DeleteGroupCommand request, CancellationToken cancellationToken)
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

                await _unitOfWork.ChatRepository.DeleteAsync(group, cancellationToken);

                if (!group.IsPrivate.Value) await _unitOfWork.MentionRepository.DeleteAsync(group.Mention);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return Unit.Value;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
