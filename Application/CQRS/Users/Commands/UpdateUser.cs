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
        private readonly IUnitOfWork _unitOfWork;

        public UpdateUserCommandHandler(
            IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<UserVm> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);
                if (user == null) throw new Exception("User not found");

                user.Fullname = request.Fullname;

                if (user.Mention.Shortname != request.Shortname)
                {
                    var mention = await _unitOfWork.MentionRepository.GetByShortnameAsync(request.Shortname, cancellationToken);
                    if (mention != null) throw new Exception("Shortname had taken");

                    await _unitOfWork.MentionRepository.DeleteAsync(user.Mention, cancellationToken);

                    mention = new UserMention
                    {
                        Shortname = request.Shortname,
                        UserId = user.Id,
                        User = user,
                    };

                    await _unitOfWork.MentionRepository.AddAsync(mention, cancellationToken);
                }

                await _unitOfWork.UserRepository.UpdateAsync(user, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new UserVm
                {
                    Fullname = request.Fullname,
                    Shortname = request.Shortname,
                };
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
