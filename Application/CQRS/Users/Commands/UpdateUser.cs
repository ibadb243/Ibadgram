using Application.Interfaces.Repositories;
using Domain.Entities;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Users.Commands.UpdateUser
{
    public class UserVm
    {
        public string Fullname { get; set; }
        public string Shortname { get; set; }
    }

    public class UpdateUserCommand : IRequest<UserVm>
    {
        public Guid UserId { get; set; }
        public string Fullname { get; set; }
        public string Shortname { get; set; }
    }

    public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
    {
        public UpdateUserCommandValidator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.Fullname).NotEmpty().MaximumLength(256);
            RuleFor(x => x.Shortname).NotEmpty().MinimumLength(4).MaximumLength(64);
        }
    }

    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserVm>
    {
        private readonly IUserRepository _userRepository;
        private readonly IMentionRepository _mentionRepository;
        private readonly IUnitOfWork _unitOfWork;

        public UpdateUserCommandHandler(
            IUserRepository userRepository,
            IMentionRepository mentionRepository,
            IUnitOfWork unitOfWork)
        {
            _userRepository = userRepository;
            _mentionRepository = mentionRepository;
            _unitOfWork = unitOfWork;
        }

        public async Task<UserVm> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null) throw new Exception("User not found");

            user.Fullname = request.Fullname;

            if (user.Mention.Shortname != request.Shortname)
            {
                var mention = await _mentionRepository.GetByShortnameAsync(request.Shortname, cancellationToken);
                if (mention != null) throw new Exception("Shortname had taken");

                await _mentionRepository.DeleteAsync(user.Mention, cancellationToken);

                mention = new UserMention
                {
                    Shortname = request.Shortname,
                    UserId = user.Id,
                    User = user,
                };

                await _mentionRepository.AddAsync(mention, cancellationToken);
            }

            await _userRepository.UpdateAsync(user, cancellationToken);

            await _unitOfWork.SaveAsync(cancellationToken);

            return new UserVm
            {
                Fullname = request.Fullname,
                Shortname = request.Shortname,
            };
        }
    }
}
