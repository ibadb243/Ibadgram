using Domain.Common;
using Domain.Enums;

namespace Domain.Entities
{
    public class ChatMember : IHasCreationTime, IHasModificationTime
    {
        public Guid ChatId { get; set; }
        public Guid UserId { get; set; }
        public string? Nickname { get; set; } = null;
        public ChatRole? Role { get; set; }
        public Chat Chat { get; set; }
        public User User { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
