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

namespace Application.CQRS.Users.Commands.UpdateShortname
{
    public class UpdateShortnameCommand : IRequest<Result>
    {
        public Guid UserId { get; set; }
        public string Shortname { get; set; }
    }

    public class UpdateShortnameCommandValidator : AbstractValidator<UpdateShortnameCommand>
    {
        private readonly IUserRepository _userRepository;
        private readonly IMentionRepository _mentionRepository;

        public UpdateShortnameCommandValidator(
            IUserRepository userRepository,
            IMentionRepository mentionRepository)
        {
            _userRepository = userRepository;
            _mentionRepository = mentionRepository;

            RuleFor(x => x.UserId)
                .NotEmpty()
                .MustAsync(BeExist)
                .WithMessage("User not found")
                .MustAsync(BeVerified)
                .WithMessage("User do not pass registration");

            RuleFor(x => x.Shortname)
                .NotEmpty()
                .MinimumLength(ShortnameConstants.MinLength)
                .MaximumLength(ShortnameConstants.MaxLength)
                .MustAsync(BeFree)
                .WithMessage("Shortname has already taken");
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

        private async Task<bool> BeFree(string shortname, CancellationToken cancellationToken)
        {
            return await _mentionRepository.ExistsByShortnameAsync(shortname, cancellationToken);
        }
    }

    public class UpdateShortnameCommandHandler : IRequestHandler<UpdateShortnameCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateShortnameCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(UpdateShortnameCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);
                if (user == null) return Result.Fail($"User with ID {request.UserId} not found");

                await _unitOfWork.MentionRepository.DeleteAsync(user.Mention.Id, cancellationToken);

                var mention = new UserMention
                {
                    Id = request.UserId,
                    Shortname = request.Shortname,
                    UserId = user.Id
                };

                await _unitOfWork.MentionRepository.AddAsync(mention, cancellationToken);

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return Result.Ok();
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
