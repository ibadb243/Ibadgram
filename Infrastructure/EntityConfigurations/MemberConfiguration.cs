using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.EntityConfigurations
{
    public class MemberConfiguration : IEntityTypeConfiguration<ChatMember>
    {
        public void Configure(EntityTypeBuilder<ChatMember> builder)
        {
            // Table name
            builder.ToTable("ChatMembers");

            // Composite primary key using ChatId and UserId
            builder
                .HasKey(x => new { x.ChatId, x.UserId });

            // Required properties
            builder
                .Property(x => x.ChatId)
                .IsRequired();

            builder
                .Property(x => x.UserId)
                .IsRequired();

            // Nickname configuration with max length
            builder
                .Property(x => x.Nickname)
                .HasMaxLength(64)
                .IsRequired(false); // Nullable

            // Role configuration (nullable enum)
            builder
                .Property(x => x.Role)
                .IsRequired(false);

            // Timestamp configurations
            builder
                .Property(x => x.CreatedAtUtc);

            builder
                .Property(x => x.UpdatedAtUtc)
                .IsRequired(false);

            // Relationships
            builder
                .HasOne(x => x.Chat)
                .WithMany() // Assuming Chat has a collection of ChatMembers
                .HasForeignKey(x => x.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(x => x.User)
                .WithMany() // Assuming User has a collection of ChatMembers
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
