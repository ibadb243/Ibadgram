namespace Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Firstname { get; set; }
        public string? Lastname { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public UserMention Mention { get; set; }
        public List<ChatMember> Memberships { get; set; }
        public List<Message> Messages { get; set; }
        public List<RefreshToken> RefreshTokens { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool IsDeleted { get; set; }
    }
}
