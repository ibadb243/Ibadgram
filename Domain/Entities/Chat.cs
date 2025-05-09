using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class Chat : BaseEntity
    {
        public string Name { get; set; }
        public string? Description { get; set; }
        public List<Member> Members { get; set; }
        public List<Message> Messages { get; set; }
    }
}
