using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace WebAPI.Controllers
{
    [ApiController]
    public abstract class BaseController : ControllerBase
    {
        private IMediator _mediator;
        protected IMediator Mediator => _mediator ??= HttpContext.RequestServices.GetService<IMediator>()!;

        protected Guid UserId => GetUserId();

        protected BaseController(IMediator mediator)
        {
            _mediator = mediator;
        }

        private Guid GetUserId()
        {
            if (User.Identity?.IsAuthenticated == true)
                if (Guid.TryParse((User.FindFirst(JwtRegisteredClaimNames.Sid)?.Value) ?? "", out Guid userId))
                    return userId;

            return Guid.Empty;
        }
    }
}
