namespace Domain.Entities
{
    public class Message
    {
        public Guid ChatId { get; set; }
        public long Id { get; set; }
        public Guid UserId { get; set; }
        public string Text { get; set; }
        public Chat Chat { get; set; }
        public User User { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public bool IsDeleted { get; set; }
    }
}
