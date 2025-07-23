using Domain.Common.Constants;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Persistence.EntityConfigurations
{
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            // Table name
            builder
                .ToTable("Users");

            // Primary Key
            builder
                .HasKey(x => x.Id);

            // Indexes
            builder
                .HasIndex(x => x.Email)
                .IsUnique();

            // Id
            builder
                .Property(x => x.Id)
                .ValueGeneratedOnAdd();

            // Avatar
            builder
                .Property(x => x.Avatar)
                .IsRequired(false)
                .HasMaxLength(500); // URL или путь к файлу

            // Firstname
            builder
                .Property(x => x.Firstname)
                .IsRequired()
                .HasMaxLength(UserConstants.FirstnameMaxLength);

            // Lastname
            builder
                .Property(x => x.Lastname)
                .IsRequired(false)
                .HasMaxLength(UserConstants.LastnameLength);

            // Bio
            builder
                .Property(x => x.Bio)
                .IsRequired(false)
                .HasMaxLength(UserConstants.BioLength);

            // Status (enum)
            builder
                .Property(x => x.Status)
                .IsRequired()
                .HasConversion<int>(); // Сохраняется как integer в БД

            // LastSeenAt
            builder
                .Property(x => x.LastSeenAt)
                .IsRequired(false);

            // Email
            builder
                .Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(320); // RFC 5321 максимальная длина email

            // EmailConfirmed
            builder
                .Property(x => x.EmailConfirmed)
                .IsRequired()
                .HasDefaultValue(false);

            // EmailConfirmationToken
            builder
                .Property(x => x.EmailConfirmationToken)
                .IsRequired(false)
                .HasMaxLength(256);

            // EmailConfirmationTokenExpiry
            builder
                .Property(x => x.EmailConfirmationTokenExpiry)
                .IsRequired(false);

            // PhoneNumber
            builder
                .Property(x => x.PhoneNumber)
                .IsRequired(false)
                .HasMaxLength(20);

            // PhoneConfirmed
            builder
                .Property(x => x.PhoneConfirmed)
                .IsRequired()
                .HasDefaultValue(false);

            // PasswordSalt
            builder
                .Property(x => x.PasswordSalt)
                .IsRequired()
                .HasMaxLength(128);

            // PasswordHash
            builder
                .Property(x => x.PasswordHash)
                .IsRequired()
                .HasMaxLength(256);

            // TimeZone
            builder
                .Property(x => x.TimeZone)
                .IsRequired(false)
                .HasMaxLength(50);

            // Language
            builder
                .Property(x => x.Language)
                .IsRequired(false)
                .HasMaxLength(10); // Например, "en-US", "ru-RU"

            // TwoFactorEnabled
            builder
                .Property(x => x.TwoFactorEnabled)
                .IsRequired()
                .HasDefaultValue(false);

            // FailedLoginAttempts
            builder
                .Property(x => x.FailedLoginAttempts)
                .IsRequired()
                .HasDefaultValue(0);

            // LockoutEnd
            builder
                .Property(x => x.LockoutEnd)
                .IsRequired(false);

            // IsVerified
            builder
                .Property(x => x.IsVerified)
                .IsRequired()
                .HasDefaultValue(false);

            // CreatedAtUtc
            builder
                .Property(x => x.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("getdate()");

            // IsDeleted (для soft delete)
            builder
                .Property(x => x.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            // Navigation property

            // UserMention (один к одному)
            builder
                .HasOne(u => u.Mention)
                .WithOne()
                .HasForeignKey<UserMention>(m => m.UserId);

            // ChatMember (один ко многим)
            builder
                .HasMany(u => u.Memberships)
                .WithOne(m => m.User)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // RefreshToken (один ко многим)
            builder
                .HasMany(u => u.RefreshTokens)
                .WithOne(rt => rt.User)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Глобальный фильтр для soft delete
            builder
                .HasQueryFilter(u => !u.IsDeleted);
        }
    }
}
