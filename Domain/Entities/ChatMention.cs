using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class ChatMention : Mention
    {
        public Guid ChatId { get; set; }
        public Chat Chat { get; set; }
    }
}
