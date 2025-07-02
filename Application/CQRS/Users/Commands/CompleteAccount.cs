using Application.Interfaces.Repositories;
using Domain.Common.Constants;
using Domain.Entities;
using FluentResults;
using FluentValidation;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Users.Commands.CompleteAccount
{
    public class CompleteAccountCommand : IRequest<Result<Guid>>
    {
        public Guid UserId { get; set; }
        public string Shortname { get; set; }
        public string? Bio { get; set; }
    }

    public class CompleteAccountCommandValidator : AbstractValidator<CompleteAccountCommand>
    {
        private readonly IMentionRepository _mentionRepository;

        public CompleteAccountCommandValidator(IMentionRepository mentionRepository)
        {
            _mentionRepository = mentionRepository;

            RuleFor(x => x.UserId)
                .NotEmpty();

            RuleFor(x => x.Shortname)
                .NotEmpty()
                .MinimumLength(ShortnameConstants.MinLength)
                .MaximumLength(ShortnameConstants.MaxLength)
                .MustAsync(BeUniqueShortname)
                .WithMessage($"Shortname has already taken");

            RuleFor(x => x.Bio)
                .MaximumLength(UserConstants.BioLength);
        }

        private async Task<bool> BeUniqueShortname(string shortname, CancellationToken cancellationToken)
        {
            return !await _mentionRepository.ExistsByShortnameAsync(shortname, cancellationToken);
        }
    }

    public class CompleteAccountCommandHandler : IRequestHandler<CompleteAccountCommand, Result<Guid>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public CompleteAccountCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result<Guid>> Handle(CompleteAccountCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);
                if (user == null) return Result.Fail("User not found");

                user.Bio = request.Bio;
                user.IsVerified = true;
                
                await _unitOfWork.UserRepository.UpdateAsync(user, cancellationToken);

                var mention = new UserMention
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Shortname = request.Shortname,
                };

                await _unitOfWork.MentionRepository.AddAsync(mention, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return user.Id;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
