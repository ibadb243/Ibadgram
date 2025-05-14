using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class Message : BaseEntity
    {
        public Guid ChatId { get; set; }
        public Guid UserId { get; set; }
        public string Text { get; set; } = string.Empty;
        public Chat Chat { get; set; }
        public User User { get; set; }
    }
}
