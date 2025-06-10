using Application.Interfaces.Repositories;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Messages.Commands.UpdateMessage
{
    public class UpdateMessageCommand : IRequest<Unit>
    {
        public Guid UserId { get; set; }
        public Guid ChatId { get; set; }
        public Guid MessageId { get; set; }
        public string Message { get; set; }
    }

    public class UpdateMessageCommandValidator : AbstractValidator<UpdateMessageCommand>
    {
        public UpdateMessageCommandValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.ChatId).NotEmpty();
            RuleFor(x => x.MessageId).NotEmpty();
            RuleFor(x => x.Message).NotEmpty().MinimumLength(1);
        }
    }

    public class UpdateMessageCommandHandler : IRequestHandler<UpdateMessageCommand, Unit>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateMessageCommandHandler(
            IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Unit> Handle(UpdateMessageCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);
                if (user == null) throw new Exception("User not found");

                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(request.ChatId, cancellationToken);
                if (chat == null) throw new Exception("Chat not found");

                var member = await _unitOfWork.ChatMemberRepository.GetByIdsAsync(request.ChatId, request.UserId, cancellationToken);
                if (member == null) throw new Exception("Yoe are not member of chat");

                var message = await _unitOfWork.MessageRepository.GetByIdAsync(request.MessageId, cancellationToken);
                if (message == null) throw new Exception("Message not found");

                if (message.UserId != request.UserId) throw new Exception("User can edit only own messages");

                message.Text = request.Message;

                await _unitOfWork.MessageRepository.UpdateAsync(message, cancellationToken);
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
