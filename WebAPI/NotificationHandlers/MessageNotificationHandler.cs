using Application.CQRS.Messages.Notifications;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using WebAPI.Hubs;

namespace WebAPI.NotificationHandlers
{
    public class MessageNotificationHandler : INotificationHandler<NessageSentNotification>
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<MessageNotificationHandler> _logger;

        public MessageNotificationHandler(
            IHubContext<NotificationHub> hubContext,
            ILogger<MessageNotificationHandler> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        public async Task Handle(NessageSentNotification notification, CancellationToken cancellationToken)
        {
            try
            {
                await _hubContext.Clients.Group(notification.ChatId.ToString())
                    .SendAsync("MessageSent", new
                    {
                        userId = notification.UserId,
                        messageId =  notification.MessageId,
                    }, cancellationToken);

                _logger.LogInformation("Send notification to group {ChatId}: user {UserId} sent message {MessageId}",
                    notification.ChatId, notification.UserId, notification.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to group {ChatId}", notification.ChatId);
            }
        }
    }
}
