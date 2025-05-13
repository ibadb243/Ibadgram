using Domain.Common;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class GroupMember : BaseEntity
    {
        public Guid GroupId { get; set; }
        public Guid UserId { get; set; }
        public string? Nickname { get; set; } = null;
        public ChatRole Role { get; set; }
        public Group Group { get; set; }
        public User User { get; set; }
    }
}
