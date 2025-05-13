using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class Chat : BaseChat
    {
        public Guid User1Id { get; set; }
        public Guid User2Id { get; set; }
        public User User1 { get; set; }
        public User User2 { get; set; }
        public List<Message> Messages { get; set; }
    }
}
