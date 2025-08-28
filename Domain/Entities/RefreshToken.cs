using Domain.Common;

namespace Domain.Entities
{
    public class RefreshToken : IHasCreationTime
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public bool IsRevoked { get; set; }
        public string? UserAgent { get; set; }
        public string? DeviceId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public User User { get; set; }
    }
}
