using Domain.Enums;

namespace Domain.Entities
{
    public class Chat
    {
        public Guid Id { get; set; }
        public ChatType Type { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public bool? IsPrivate { get; set; }
        public ChatMention? Mention { get; set; }
        public List<ChatMember> Members { get; set; } = [];
        public List<Message> Messages { get; set; } = [];
        public DateTime CreatedAtUtc { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}
