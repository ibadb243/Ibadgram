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

namespace Application.CQRS.Chats.Queries
{
    public class ChatVm
    {
        public bool? IsDeleted { get; set; }
    }

    public class PersonalChatVm : ChatVm
    {
        public int MessageCount { get; set; }
    }

    public class OneToOneChatVm : ChatVm
    {
        public string Firstname { get; set; }
        public string? Lastname { get; set; }
        public string Description { get; set; }
        public string Shortname { get; set; }
    }

    public class GroupChatVm : ChatVm
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Shortname { get; set; }
        public int MemberCount { get; set; }
        public bool IsPrivate { get; set; }
    }

    public class GetChatQuery : IRequest<ChatVm>
    {
        public Guid UserId { get; set; }
        public Guid ChatId { get; set; }
    }

    public class GetChatQueryValidator : AbstractValidator<GetChatQuery>
    {
        public GetChatQueryValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.ChatId).NotEmpty();
        }
    }

    public class GetChatQueryHandler : IRequestHandler<GetChatQuery, ChatVm>
    {
        private readonly IUserRepository _userRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IChatMemberRepository _chatMemberRepository;

        public GetChatQueryHandler(
            IUserRepository userRepository,
            IChatRepository chatRepository,
            IChatMemberRepository chatMemberRepository)
        {
            _userRepository = userRepository;
            _chatRepository = chatRepository;
            _chatMemberRepository = chatMemberRepository;
        }

        public async Task<ChatVm> Handle(GetChatQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null) throw new Exception("User not found");

            var chat = await _chatRepository.GetByIdAsync(request.ChatId, cancellationToken);
            if (chat == null) throw new Exception("Chat not found");

            if (chat.IsDeleted) return new ChatVm { IsDeleted = true };

            if (chat.Type != ChatType.Group)
            {
                var member = await _chatMemberRepository.GetByIdsAsync(chat.Id, user.Id, cancellationToken);
                if (member == null) throw new Exception("Access was denied");
            }

            switch (chat.Type)
            {
                case ChatType.Personal: return new PersonalChatVm { MessageCount = chat.Messages.Count() };
                case ChatType.OneToOne:
                    {
                        var other = chat.Members.FirstOrDefault(m => m.UserId != user.Id);
                        if (other == null) throw new Exception("Hackers atack");
                        if (other.User.IsDeleted) return new ChatVm { IsDeleted = true };

                        return new OneToOneChatVm
                        {
                            Firstname = other.User.Firstname,
                            Lastname = other.User.Lastname,
                            Description = "Not yet",
                            Shortname = other.User.Mention.Shortname,
                        };
                    }
                case ChatType.Group: return new GroupChatVm
                {
                    Name = chat.Name,
                    Description = chat.Description,
                    Shortname = chat.Mention.Shortname,
                    MemberCount = chat.Members.Count()
                };
                default: return new ChatVm { IsDeleted = true };
            }
        }
    }
}
