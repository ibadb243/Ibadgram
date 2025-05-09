using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class Member : BaseEntity
    {
        public Guid ChatId { get; set; }
        public Guid UserId { get; set; }
        public string? Nickname { get; set; } = null;
        public ChatRole Role { get; set; }
        public Chat Chat { get; set; }
        public User User { get; set; }
    }
}
