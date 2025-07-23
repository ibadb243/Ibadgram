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
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            // Table name
            builder
                .ToTable("RefreshTokens");

            // Primary Key
            builder
                .HasKey(x => x.Id);

            // Id
            builder
                .Property(x => x.Id)
                .ValueGeneratedOnAdd();

            // UserId
            builder
                .Property(x => x.UserId)
                .IsRequired();

            // Token
            builder
                .Property(x => x.Token)
                .IsRequired()
                .HasMaxLength(512); // Достаточно для JWT токенов

            // IsRevoked
            builder
                .Property(x => x.IsRevoked)
                .IsRequired()
                .HasDefaultValue(false);

            // UserAgent
            builder
                .Property(x => x.UserAgent)
                .IsRequired(false)
                .HasMaxLength(1000); // User-Agent может быть длинным

            // DeviceId
            builder
                .Property(x => x.DeviceId)
                .IsRequired(false)
                .HasMaxLength(128);

            // CreatedAtUtc
            builder
                .Property(x => x.CreatedAtUtc)
                .IsRequired()
                .HasDefaultValueSql("getdate()");

            // ExpiresAtUtc
            builder
                .Property(x => x.ExpiresAtUtc)
                .IsRequired();

            // Indexes
            builder
                .HasIndex(x => x.UserId)
                .HasDatabaseName("IX_RefreshTokens_UserId");

            builder
                .HasIndex(x => x.Token)
                .IsUnique()
                .HasDatabaseName("IX_RefreshTokens_Token");

            builder
                .HasIndex(x => x.ExpiresAtUtc)
                .HasDatabaseName("IX_RefreshTokens_ExpiresAtUtc");

            // Составной индекс для поиска активных токенов пользователя
            builder
                .HasIndex(x => new { x.UserId, x.IsRevoked, x.ExpiresAtUtc })
                .HasDatabaseName("IX_RefreshTokens_UserId_IsRevoked_ExpiresAtUtc");

            // Навигационные свойства

            // User (многие к одному)
            builder
                .HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
