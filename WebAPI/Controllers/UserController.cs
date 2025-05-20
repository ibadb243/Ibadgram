using Application.CQRS.Users.Commands.UpdateUser;
using Application.CQRS.Users.Queries;
using Application.CQRS.Users.Queries.Get;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/users/")]
    public class UserController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UserController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("me/")]
        public async Task<IActionResult> GetMe(
            CancellationToken cancellationToken)
        {
            var userId = HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sid);
            if (userId == null) return Unauthorized();

            var query = new GetUserQuery
            {
                UserId = Guid.Parse(userId.Value)
            };

            var result = await _mediator.Send(query, cancellationToken);

            return Ok(result);
        }

        [HttpGet("{userId:guid}")]
        public async Task<IActionResult> Get(
            CancellationToken cancellationToken,
            [FromRoute] string userId)
        {
            var query = new GetUserQuery
            {
                UserId = Guid.Parse(userId)
            };

            var result = await _mediator.Send(query, cancellationToken);

            return Ok(result);
        }

        [HttpGet("memberships/")]
        public async Task<IActionResult> GetMemberships(
            CancellationToken cancellationToken)
        {
            var userId = HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sid);
            if (userId == null) return Unauthorized();

            var query = new GetUserMembershipsQuery
            {
                UserId = Guid.Parse(userId.Value)
            };

            var result = await _mediator.Send(query, cancellationToken);

            return Ok(result);
        }

        [HttpPut]
        public async Task<IActionResult> Update(
            CancellationToken cancellationToken,
            [FromBody] string? fullname,
            [FromBody] string? shortname)
        {
            var userId = HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sid);
            if (userId == null) return Unauthorized();

            var command = new UpdateUserCommand
            {
                UserId = Guid.Parse(userId.Value),
                Fullname = fullname,
                Shortname = shortname,
            };

            var result = await _mediator.Send(command, cancellationToken);

            return Ok(result);
        }
    }
}
