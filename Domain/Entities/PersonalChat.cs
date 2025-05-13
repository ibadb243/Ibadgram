using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class PersonalChat : ChatRoom
    {
        public Guid UserId { get; set; }
        public User User { get; set; }
        public List<Message> Messages { get; set; }
    }
}
