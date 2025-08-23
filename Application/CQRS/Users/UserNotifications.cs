using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.CQRS.Users.Notifications
{
    public class UserLoggedInNotification : INotification
    {
        public Guid UserId { get; set; }
        public DateTime LoggedInAtUtc { get; set; }
    }

    public class UserLoggedOutNotification : INotification
    {
        public Guid UserId { get; set; }
        public DateTime LoggedOutAtUtc { get; set; }
    }
}
