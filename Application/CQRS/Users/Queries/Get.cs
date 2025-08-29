using Domain.Common;
using Domain.Enums;
using Domain.Errors;
using Domain.Repositories;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CQRS.Users.Queries.Get
{
    public class GetUserQuery : IRequest<Result<GetUserQueryResponse>>
    {
        public Guid UserId { get; set; }
    }

    public class GetUserQueryResponse
    {
        public string Firstname { get; set; } = string.Empty;
        public string? Lastname { get; set; }
        public string Shortname { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public UserStatus Status { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public bool? IsDeleted { get; set; }
    }

    public class GetUserQueryValidator : AbstractValidator<GetUserQuery>
    {
        public GetUserQueryValidator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("UserId is required");
        }
    }

    public class GetUserQueryHandler : IRequestHandler<GetUserQuery, Result<GetUserQueryResponse>>
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

        public async Task<Result<GetUserQueryResponse>> Handle(GetUserQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Processing get user request for UserId: {UserId}", request.UserId);

            try
            {
                _logger.LogDebug("Retrieving user by id {UserId} from database", request.UserId);
                var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);

                if (user == null)
                {
                    _logger.LogWarning("User with id {UserId} not found", request.UserId);
                    return Result.Fail(new BusinessLogicError(
                        ErrorCodes.USER_NOT_FOUND,
                        "User not found",
                        new { UserId = request.UserId }));
                }

                _logger.LogDebug("User {UserId} found with status: {Status}, IsDeleted: {IsDeleted}",
                    request.UserId, user.Status, user.IsDeleted);

                if (user.IsDeleted)
                {
                    _logger.LogInformation("User {UserId} is marked as deleted", request.UserId);
                    return Result.Ok(new GetUserQueryResponse
                    {
                        Firstname = "DELETED",
                        IsDeleted = true
                    });
                }

                _logger.LogDebug("Mapping user data for UserId: {UserId}", request.UserId);
                var response = new GetUserQueryResponse
                {
                    Firstname = user.Firstname,
                    Lastname = user.Lastname,
                    Shortname = user.Mention?.Shortname ?? string.Empty,
                    Bio = user.Bio,
                    Status = user.Status,
                    LastSeenAt = user.LastSeenAt,
                    IsDeleted = user.IsDeleted
                };

                _logger.LogInformation("Successfully retrieved user data for UserId: {UserId}", request.UserId);
                return Result.Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing get user query for UserId: {UserId}", request.UserId);
                return Result.Fail($"An error occurred while retrieving user: {ex.Message}");
            }
        }
    }
}
