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

namespace Application.CQRS.Users.Commands.UpdateUser
{
    public class UpdateUserCommand : IRequest<Result>
    {
        public Guid UserId { get; set; }
        public string? Firstname { get; set; }
        public string? Lastname { get; set; }
        public string? Bio { get; set; }
    }

    public class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
    {
        private readonly IUserRepository _userRepository;

        public UpdateUserCommandValidator(IUserRepository userRepository)
        {
            _userRepository = userRepository;

            RuleFor(x => x.UserId)
                .NotEmpty()
                .MustAsync(BeExist)
                .WithMessage("User not found")
                .MustAsync(BeVerifed)
                .WithMessage("User do not pass registration");

            RuleFor(x => x.Firstname)
                .MinimumLength(UserConstants.FirstnameMinLength)
                .MaximumLength(UserConstants.FirstnameMaxLength);

            RuleFor(x => x.Lastname)
                .MaximumLength(UserConstants.LastnameLength);

            RuleFor(x => x.Bio)
                .MaximumLength(UserConstants.BioLength);
        }

        private async Task<bool> BeExist(Guid userId, CancellationToken cancellationToken)
        {
            return await _userRepository.ExistsAsync(userId, cancellationToken);
        }

        private async Task<bool> BeVerifed(Guid userId, CancellationToken cancellationToken)
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            return user != null && user.IsVerified;
        }
    }

    public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateUserCommandHandler(
            IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            try
            {
                var user = await _unitOfWork.UserRepository.GetByIdAsync(request.UserId, cancellationToken);
                if (user == null) return Result.Fail($"User with ID {request.UserId} not found");

                user.Firstname = request.Firstname ?? user.Firstname;
                user.Lastname = request.Lastname ?? user.Lastname;
                user.Bio = request.Bio ?? user.Bio;
                
                await _unitOfWork.UserRepository.UpdateAsync(user, cancellationToken);
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
