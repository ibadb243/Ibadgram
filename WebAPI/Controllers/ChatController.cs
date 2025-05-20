using Application.CQRS.Chats.Commands.CreateChat;
using Application.CQRS.Chats.Commands.CreateGroup;
using Application.CQRS.Chats.Commands.DeleteGroup;
using Application.CQRS.Chats.Commands.UpdateGroup;
using Application.CQRS.Chats.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace WebAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/chats/")]
    public class ChatController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ChatController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("{chatId:guid}")]
        public async Task<IActionResult> Get(
            CancellationToken cancellationToken,
            [FromRoute] Guid chatId)
        {
            var userId = HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sid);
            if (userId == null) return Unauthorized();

            var query = new GetChatQuery
            {
                UserId = Guid.Parse(userId.Value),
                ChatId = chatId
            };

            var result = await _mediator.Send(query, cancellationToken);

            return Ok(result);
        }

        [HttpGet("{chatId:guid}")]
        public async Task<IActionResult> GetMembers(
            CancellationToken cancellationToken,
            [FromRoute] Guid chatId)
        {
            throw new NotImplementedException();
        }

        [HttpPost]
        public async Task<IActionResult> CreateChat(
            CancellationToken cancellationToken,
            [FromBody] Guid user_id)
        {
            var firstUserId = HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sid);
            if (firstUserId == null) return Unauthorized();

            var command = new CreateChatCommand
            {
                FirstUserId = Guid.Parse(firstUserId.Value),
                SecondUserId = user_id,
            };

            var result = await _mediator.Send(command, cancellationToken);

            return Ok(result);
        }

        [HttpPost]  
        public async Task<IActionResult> CreateGroup(
            CancellationToken cancellationToken,
            [FromBody] string? name,
            [FromBody] string? desc,
            [FromBody] bool? is_private,
            [FromBody] string? shortname)
        {
            var firstUserId = HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sid);
            if (firstUserId == null) return Unauthorized();

            var command = new CreateGroupCommand
            {
                UserId = Guid.Parse(firstUserId.Value),
                Name = name,
                Description = desc,
                IsPrivate = is_private ?? false,
                Shortname = shortname,
            };

            var result = await _mediator.Send(command, cancellationToken);

            return Ok(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateGroup(
            CancellationToken cancellationToken,
            [FromBody] Guid group_id,
            [FromBody] string? name,
            [FromBody] string? desc,
            [FromBody] bool? is_private,
            [FromBody] string? shortname)
        {
            var userId = HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sid);
            if (userId == null) return Unauthorized();

            var command = new UpdateGroupCommand
            {
                UserId = Guid.Parse(userId.Value),
                GroupId = group_id,
                Name = name,
                Description = desc,
                IsPrivate = is_private ?? false,
                Shortname = shortname,
            };

            var result = await _mediator.Send(command, cancellationToken);

            return Ok(result);
        }

        [HttpDelete("{groupId:guid}")]
        public async Task<IActionResult> DeleteGroup(
            CancellationToken cancellationToken,
            [FromRoute] Guid groupId)
        {
            var userId = HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sid);
            if (userId == null) return Unauthorized();

            var command = new DeleteGroupCommand
            {
                UserId = Guid.Parse(userId.Value),
                GroupId = groupId,
            };

            await _mediator.Send(command, cancellationToken);

            return Ok();
        }
    }
}
