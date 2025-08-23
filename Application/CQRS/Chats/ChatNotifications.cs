using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Chats.Notifications
{
    public class ChatCreatedNotification : INotification
    {
        public Guid UserId { get; set; }
        public Guid ChatId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class GroupCreatedNotification : INotification
    {
        public Guid UserId { get; set; }
        public Guid ChatId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
