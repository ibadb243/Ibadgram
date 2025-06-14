using Application.Interfaces.Repositories;
using Domain.Common.Constants;
using Domain.Entities;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Messages.Commands.SendMessage
{
    public class SendMessageCommand : IRequest<long>
    {
        public Guid UserId { get; set; }
        public Guid ChatId { get; set; }
        public string Message { get; set; }
    }

    public class SendMessageCommandValidator : AbstractValidator<SendMessageCommand>
    {
        public SendMessageCommandValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty();

            RuleFor(x => x.ChatId)
                .NotEmpty();

            RuleFor(x => x.Message)
                .NotEmpty()
                .MinimumLength(MessageConstants.MinLength)
                .MaximumLength(MessageConstants.MaxLength);
        }
    }

    public class SendMessageCommandHandler : IRequestHandler<SendMessageCommand, long>
    {
        private readonly IUnitOfWork _unitOfWork;

        public SendMessageCommandHandler(
            IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<long> Handle(SendMessageCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);
                if (user == null) throw new Exception("User not found");

                var chat = await _unitOfWork.ChatRepository.GetByIdAsync(request.ChatId, cancellationToken);
                if (chat == null) throw new Exception("Chat not found");

                var member = await _unitOfWork.ChatMemberRepository.GetByIdsAsync(request.ChatId, request.UserId, cancellationToken);
                if (member == null) throw new Exception("You are not member of chat");

                var msg = new Message
                {
                    UserId = request.UserId,
                    ChatId = request.ChatId,
                    Text = request.Message
                };

                await _unitOfWork.MessageRepository.AddAsync(msg, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return msg.Id;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
