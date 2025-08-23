using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Messages.Notifications
{
    public class NessageSentNotification : INotification
    {
        public Guid UserId { get; set; }
        public Guid ChatId { get; set; }
        public long MessageId { get; set; }
    }
}
