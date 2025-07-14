using Application.Interfaces.Repositories;
using AutoMapper;
using Domain.Enums;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
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
                    .WithMessage("UserId is required");
                //.MustAsync(BeExist)
                //.WithMessage("User not found")
                //.MustAsync(BeVerified)
                //.WithMessage("User do not pass registration");
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
        private readonly ILogger<GetUserQueryHandler> _logger;

        public GetUserQueryHandler(
            IUserRepository userRepository,
            ILogger<GetUserQueryHandler> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<Result<UserVm>> Handle(GetUserQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Proccessing get user for id: {UserId}", request.UserId);

            try
            {
                _logger.LogDebug("Retrieving user by id {UserID} from databse", request.UserId);
                var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

                if (user == null)
                {
                    _logger.LogWarning("Get user failed - user {UserId} not found", request.UserId);
                    return Result.Fail("User not found");
                }

                _logger.LogDebug("User {UserId} found with status: {Status}, IsDeleted: {IsDeleted}", request.UserId, user.Status, user.IsDeleted);

                if (user.IsDeleted) 
                {
                    _logger.LogInformation("Returning deleted user indicator for UserId: {UserId}", request.UserId);
                    return new UserVm { IsDeleted = true };
                }

                _logger.LogDebug("Mapping user data for UserId: {UserId}", request.UserId);
                var userVm = new UserVm
                {
                    Firstname = user.Firstname,
                    Lastname = user.Lastname,
                    Shortname = user.Mention.Shortname,
                    Bio = user.Bio,
                    Status = user.Status,
                    LastSeenAt = user.LastSeenAt,
                };

                _logger.LogInformation("Successfully retrieved user data for UserId: {UserId}", request.UserId);

                return userVm;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing get user query for UserId: {UserId}", request.UserId);
                throw;
            }
        }
    }
}
