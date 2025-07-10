using Application.Interfaces.Repositories;
using AutoMapper;
using Domain.Enums;
using FluentResults;
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
        public string? Lastname { get; set; }
        public string Shortname { get; set; }
        public string? Bio { get; set; }
        public UserStatus Status { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public bool? IsDeleted { get; set; }
    }

    public class GetUserQuery : IRequest<Result<UserVm>>
    {
        public Guid UserId { get; set; }
    }

    public class GetUserQueryValidator : AbstractValidator<GetUserQuery>
    {
        private readonly IUserRepository _userRepository;

        public GetUserQueryValidator(IUserRepository userRepository)
        {
            _userRepository = userRepository;

            RuleFor(x => x.UserId)
                .NotEmpty()
                .MustAsync(BeExist)
                .WithMessage("User not found")
                .MustAsync(BeVerified)
                .WithMessage("User do not pass registration");
        }

        private async Task<bool> BeExist(Guid userId, CancellationToken cancellationToken)
        {
            return await _userRepository.ExistsAsync(userId, cancellationToken);
        }

        private async Task<bool> BeVerified(Guid userId, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            return user != null && user.IsVerified;
        }
    }

    public class GetUserQueryHandler : IRequestHandler<GetUserQuery, Result<UserVm>>
    {
        private readonly IUserRepository _userRepository;

        public GetUserQueryHandler(IUserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<Result<UserVm>> Handle(GetUserQuery request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null) return Result.Fail("User not found");

            if (user.IsDeleted) return new UserVm { IsDeleted = true };

            return new UserVm
            {
                Firstname = user.Firstname,
                Lastname = user.Lastname,
                Shortname = user.Mention.Shortname,
                Bio = user.Bio,
                Status = user.Status,
                LastSeenAt = user.LastSeenAt,
            };
        }
    }
}
