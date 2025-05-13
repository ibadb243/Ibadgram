using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class GroupChatMention : Mention
    {
        public Guid GroupChatId { get; set; }
        public GroupChat GroupChat { get; set; }
    }
}
