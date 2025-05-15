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
        private readonly IUserRepository _userRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IChatMemberRepository _chatMemberRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateChatCommandHandler(
            IUserRepository userRepository,
            IChatRepository chatRepository,
            IChatMemberRepository chatMemberRepository,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _chatRepository = chatRepository;
            _chatMemberRepository = chatMemberRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<ChatVm> Handle(CreateChatCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.FirstUserId, cancellationToken);
            if (user == null) throw new Exception("User not found");

            var user2 = await _userRepository.GetByIdAsync(request.SecondUserId, cancellationToken);
            if (user2 == null) throw new Exception("User not found");

            var chat = await _chatRepository.FindOneToOneChatAsync(user.Id, user2.Id, cancellationToken);
            if (chat != null) throw new Exception("Chat already exists");

            chat = new Chat
            {
                Type = ChatType.OneToOne,
            };

            await _chatRepository.AddAsync(chat, cancellationToken);

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

            await _chatMemberRepository.AddAsync(member1, cancellationToken);
            await _chatMemberRepository.AddAsync(member2, cancellationToken);

            await _unitOfWork.SaveAsync(cancellationToken);

            return new ChatVm { ChatId = chat.Id };
        }
    }
}
