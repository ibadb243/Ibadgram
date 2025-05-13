using Application.Interfaces.Repositories;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Chats.Commands.Create
{
    public class ChatVm
    {
        public Guid ChatId { get; set; }
    }

    public class CreateChatCommand : IRequest<ChatVm>
    {
        public Guid UserId { get; set; }
    }

    public class CreateChatCommandValidator : AbstractValidator<CreateChatCommand>
    {
        public CreateChatCommandValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
        }
    }

    public class CreateChatCommandHandler : IRequestHandler<CreateChatCommand, ChatVm>
    {
        private readonly IUserRepository _userRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IUnitOfWork _unitOfWork;

        public CreateChatCommandHandler(
            IUserRepository userRepository,
            IChatRepository chatRepository,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _chatRepository = chatRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<ChatVm> Handle(CreateChatCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null) throw new Exception("User not found");


        }
    }
}
