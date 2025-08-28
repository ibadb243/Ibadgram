using Domain.Common;
using Domain.Enums;

namespace Domain.Entities
{
    public class User : ISoftDeletable, IHasCreationTime
    {
        public Guid Id { get; set; }
        public string? Avatar { get; set; }
        public string Firstname { get; set; } = string.Empty;
        public string? Lastname { get; set; }
        public string? Bio { get; set; }
        public UserStatus Status { get; set; }
        public DateTime? LastSeenAt { get; set; }
        public string Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public string? EmailConfirmationToken { get; set; }
        public DateTime? EmailConfirmationTokenExpiry { get; set; }
        public string? PhoneNumber { get; set; }
        public bool PhoneConfirmed { get; set; }
        public string PasswordSalt { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string? TimeZone { get; set; }
        public string? Language { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public bool IsVerified { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public bool IsDeleted { get; set; }

        /* Navigation Properties */
        public UserMention Mention { get; set; }
        public List<ChatMember> Memberships { get; set; }
        public List<RefreshToken> RefreshTokens { get; set; }
    }
}
