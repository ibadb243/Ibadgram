using Application.Interfaces.Repositories;
using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Chats.Commands.CreateChat
{
    public class ChatVm
    {
        public Guid ChatId { get; set; }
    }

    public class CreateChatCommand : IRequest<ChatVm>
    {
        public Guid FirstUserId { get; set; }
        public Guid SecondUserId { get; set; }
    }

    public class CreateChatCommandValidator : AbstractValidator<CreateChatCommand>
    {
        public CreateChatCommandValidator()
        {
            RuleFor(x => x.FirstUserId).NotEmpty();
            RuleFor(x => x.SecondUserId).NotEmpty();
        }
    }

    public class CreateChatCommandHandler : IRequestHandler<CreateChatCommand, ChatVm>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateChatCommandHandler(
            IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<ChatVm> Handle(CreateChatCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.FirstUserId, cancellationToken);
                if (user == null) throw new Exception("User not found");

                var user2 = await _unitOfWork.UserRepository.GetByIdAsync(request.SecondUserId, cancellationToken);
                if (user2 == null) throw new Exception("User not found");

                var chat = await _unitOfWork.ChatRepository.FindOneToOneChatAsync(user.Id, user2.Id, cancellationToken);
                if (chat != null) throw new Exception("Chat already exists");

                chat = new Chat
                {
                    Type = ChatType.OneToOne,
                };

                await _unitOfWork.ChatRepository.AddAsync(chat, cancellationToken);

                var member1 = new ChatMember
                {
                    ChatId = chat.Id,
                    UserId = user.Id,
                    Chat = chat,
                    User = user,
                };

                var member2 = new ChatMember
                {
                    ChatId = chat.Id,
                    UserId = user2.Id,
                    Chat = chat,
                    User = user2,
                };

                await _unitOfWork.ChatMemberRepository.AddAsync(member1, cancellationToken);
                await _unitOfWork.ChatMemberRepository.AddAsync(member2, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new ChatVm { ChatId = chat.Id };
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
