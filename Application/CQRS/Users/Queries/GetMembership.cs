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

namespace Application.CQRS.Users.Queries
{
    public class MembershipLoopup
    {
        public ChatType Type { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public Message LastMessage { get; set; }
    }

    public class GetUserMembershipsQuery : IRequest<List<MembershipLoopup>>
    {
        public Guid UserId { get; set; }
    }

    public class GetUserMembershipsQueryValidator : AbstractValidator<GetUserMembershipsQuery>
    {
        public GetUserMembershipsQueryValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
        }
    }

    public class GetUserMembershipsQueryHandler : IRequestHandler<GetUserMembershipsQuery, List<MembershipLoopup>>
    {
        private readonly IUserRepository _userRepository;
        private readonly IChatMemberRepository _chatMemberRepository;

        public async Task<List<MembershipLoopup>> Handle(GetUserMembershipsQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null) throw new Exception("User not found");

            var list = await _chatMemberRepository.GetByUserIdAsync(request.UserId, cancellationToken);

            return list
                .Select(m => new MembershipLoopup
                {
                    Type = m.Chat.Type,
                    Name = m.Chat.Name,
                    Description = m.Chat.Description,
                    LastMessage = m.Chat.Messages.Last()
                })
                .ToList();
        }
    }
}
