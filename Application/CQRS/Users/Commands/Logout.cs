using Application.Interfaces.Repositories;
using Domain.Common;
using FluentResults;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Users.Commands.Logout
{
    public class LogoutUserCommand : IRequest<Result>
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class LogoutUserCommandValidator : AbstractValidator<LogoutUserCommand>
    {
        public LogoutUserCommandValidator()
        {
            RuleFor(x => x.AccessToken)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("Access Token is required");

            RuleFor(x => x.RefreshToken)
                .Cascade(CascadeMode.Stop)
                .NotEmpty()
                    .WithErrorCode(ErrorCodes.REQUIRED_FIELD)
                    .WithMessage("Refresh Token is required");
        }
    }

    //public class LogoutUserCommandHandler : IRequestHandler<LogoutUserCommand, Result>
    //{
    //    private readonly IUnitOfWork _unitOfWork;
    //    private readonly ILogger<LogoutUserCommandHandler> _logger;

    //    public LogoutUserCommandHandler(
    //        IUnitOfWork unitOfWork,
    //        ILogger<LogoutUserCommandHandler> logger)
    //    {
    //        _unitOfWork = unitOfWork;
    //        _logger = logger;
    //    }

    //    public async Task<Result> Handle(LogoutUserCommand request, CancellationToken cancellationToken)
    //    {
    //        _logger.LogInformation("Starting logout proccess: {AccessToken}", request.AccessToken);

    //        await _unitOfWork.BeginTransactionAsync(cancellationToken);

    //        try
    //        {
    //            var refreshToken = await _unitOfWork.RefreshTokenRepository.GetByTokenAsync(request.RefreshToken);
    //            if (refreshToken == null)
    //            {

    //            }
    //        }
    //        catch (Exception ex)
    //        {

    //            throw;
    //        }
    //    }
    //}
}
