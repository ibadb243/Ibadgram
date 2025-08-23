using Application.CQRS.Users.Notifications;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using WebAPI.Hubs;

namespace WebAPI.NotificationHandlers
{
    public class UserNotificationHandler :
        INotificationHandler<UserLoggedInNotification>,
        INotificationHandler<UserLoggedOutNotification>
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<UserNotificationHandler> _logger;

        public UserNotificationHandler(
            IHubContext<NotificationHub> hubContext,
            ILogger<UserNotificationHandler> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task Handle(UserLoggedInNotification notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("User logged in: {UserId}", notification.UserId);
        }

        public async Task Handle(UserLoggedOutNotification notification, CancellationToken cancellationToken)
        {
            _logger.LogInformation("User logged out: {UserId}", notification.UserId);
        }
    }
}
