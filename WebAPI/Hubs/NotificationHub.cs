using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace WebAPI.Hubs
{
    [Authorize(Policy = "Standart")]
    public class NotificationHub : Hub
    {
        private readonly IMediator _mediator;
        private readonly ILogger<NotificationHub> _logger;

        public NotificationHub(
            IMediator mediator,
            ILogger<NotificationHub> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogError(exception, "Coonection disconnected");
            await base.OnDisconnectedAsync(exception);
        }
    }
}
