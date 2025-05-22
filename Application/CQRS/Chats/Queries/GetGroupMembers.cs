using Application.Interfaces.Repositories;
using Domain.Enums;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Chats.Queries.GetGroupMembers
{
    public class MemberLoopup
    {
        public string Fullname { get; set; }
        public ChatRole Role { get; set; }
        public string? Nickname { get; set; }
    }

    public class GetGroupMembersQuery : IRequest<List<MemberLoopup>>
    {
        public Guid UserId { get; set; }
        public Guid ChatId { get; set; }
        public int Offset { get; set; } = 0;
        public int Limit { get; set; } = 50;
    }

    public class GetGroupMembersQueryValidator : AbstractValidator<GetGroupMembersQuery>
    {
        public GetGroupMembersQueryValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.ChatId).NotEmpty();
            RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
            RuleFor(x => x.Limit).GreaterThanOrEqualTo(1).LessThanOrEqualTo(200);
        }
    }

    public class GetGroupMembersQueryHandler : IRequestHandler<GetGroupMembersQuery, List<MemberLoopup>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IChatRepository _chatRepository;
        private readonly IChatMemberRepository _chatMemberRepository;

        public GetGroupMembersQueryHandler(
            IUserRepository userRepository,
            IChatRepository chatRepository,
            IChatMemberRepository chatMemberRepository)
        {
            _userRepository = userRepository;
            _chatRepository = chatRepository;
            _chatMemberRepository = chatMemberRepository;
        }

        public async Task<List<MemberLoopup>> Handle(GetGroupMembersQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null) throw new Exception("User not found");

            var group = await _chatRepository.GetByIdAsync(request.ChatId, cancellationToken);
            if (group == null) throw new Exception("Group not found");

            var member = await _chatMemberRepository.GetByIdsAsync(group.Id, user.Id, cancellationToken);
            if (member == null) throw new Exception("You are not member");

            var members = await _chatMemberRepository.GetByChatIdAsync(request.ChatId, cancellationToken);

            return members
                .Skip(request.Offset)
                .Take(request.Limit)
                .Select(m => new MemberLoopup
                {
                    Fullname = m.User.Fullname,
                    Nickname = m.Nickname,
                    Role = m.Role ?? ChatRole.Member,
                })
                .ToList();
        }
    }
}
