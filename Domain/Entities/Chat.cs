using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class Chat : BaseEntity
    {
        public ChatType Type { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsPrivate { get; set; }
        public ChatMention? Mention { get; set; }
        public List<ChatMember> Members { get; set; }
        public List<Message> Messages { get; set; }
    }
}
