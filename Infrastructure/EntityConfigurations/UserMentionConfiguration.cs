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
    public class UserMentionConfiguration : IEntityTypeConfiguration<UserMention>
    {
        public void Configure(EntityTypeBuilder<UserMention> builder)
        {
            // Configure the foreign key property
            builder
                .Property(um => um.UserId)
                .IsRequired();

            // Index on UserId for better query performance
            builder
                .HasIndex(um => um.UserId);

            // Relationship configuration
            // Note: Changed to WithMany() assuming User can have multiple mentions
            // If it's truly one-to-one, keep WithOne()
            builder
                .HasOne(um => um.User)
                .WithOne(u => u.Mention) // or WithOne(u => u.Mention) if one-to-one
                .HasForeignKey<UserMention>(um => um.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
