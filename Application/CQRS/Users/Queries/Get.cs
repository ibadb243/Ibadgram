using Application.Interfaces.Repositories;
using AutoMapper;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Users.Queries.Get
{
    public class UserVm
    {
        public string Firstname { get; set; }
        public string Lastname { get; set; }
        public string Shortname { get; set; }
        public bool? IsDeleted { get; set; }
    }

    public class GetUserQuery : IRequest<UserVm>
    {
        public Guid UserId { get; set; }
    }

    public class GetUserQueryValidator : AbstractValidator<GetUserQuery>
    {
        public GetUserQueryValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
        }
    }

    public class GetUserQueryHandler : IRequestHandler<GetUserQuery, UserVm>
    {
        private readonly IUserRepository _userRepository;

        public GetUserQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<UserVm> Handle(GetUserQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null) throw new Exception("User not found");

            if (user.IsDeleted) return new UserVm { IsDeleted = true };

            return new UserVm
            {
                Firstname = user.Firstname,
                Lastname = user.Lastname,
                Shortname = user.Mention.Shortname,
            };
        }
    }
}
