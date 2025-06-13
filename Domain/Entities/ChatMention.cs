using Domain.Common;

namespace Domain.Entities
{
    public class ChatMention : Mention
    {
        public Guid ChatId { get; set; }
        public Chat Chat { get; set; }
    }
}
