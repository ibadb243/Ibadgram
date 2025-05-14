using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class User : BaseEntity
    {
        public string Fullname { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public UserMention Mention { get; set; }
        public List<ChatMember> Memberships { get; set; }
        public List<Message> Messages { get; set; }
        public List<RefreshToken> RefreshTokens { get; set; }
    }
}
