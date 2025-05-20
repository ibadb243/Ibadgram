using Application.CQRS.Users.Commands.Login;
using Application.CQRS.Users.Commands.Refresh;
using Application.CQRS.Users.Commands.Register;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{

    [ApiController]
    [Route("api/auth/")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("register/")]
        public async Task<IActionResult> RegisterAsync(
            CancellationToken cancellationToken,
            [FromBody] string? fullname,
            [FromBody] string? email,
            [FromBody] string? shortname,
            [FromBody] string? password)
        {
            var command = new RegisterUserCommand
            {
                Fullname = fullname,
                Email = email,
                Shortname = shortname,
                Password = password
            };
            
            var result = await _mediator.Send(command, cancellationToken);

            return Ok(result);
        }

        [HttpPost("login/")]
        public async Task<IActionResult> LoginAsync(
            CancellationToken cancellationToken,
            [FromBody] string? email,
            [FromBody] string? password)
        {
            var command = new LoginUserCommand
            {
                Email = email,
                Password = password
            };

            var result = await _mediator.Send(command, cancellationToken);

            return Ok(result);
        }

        [HttpPost("refresh/")]
        public async Task<IActionResult> RefreshTokenAsync(
            CancellationToken cancellationToken,
            [FromBody] string? refresh_token)
        {
            var command = new RefreshTokenCommand
            {
                RefreshToken = refresh_token
            };

            var result = await _mediator.Send(command, cancellationToken);

            return Ok(result);
        }
    }
}
