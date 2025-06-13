using Domain.Common;

namespace Domain.Entities
{
    public class UserMention : Mention
    {
        public Guid UserId { get; set; }
        public User User { get; set; }
    }
}
