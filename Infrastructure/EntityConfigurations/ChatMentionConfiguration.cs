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
    public class ChatMentionConfiguration : IEntityTypeConfiguration<ChatMention>
    {
        public void Configure(EntityTypeBuilder<ChatMention> builder)
        {
            // Configure the foreign key relationship
            builder
                .Property(cm => cm.ChatId)
                .IsRequired();

            // Index on ChatId for better query performance
            builder
                .HasIndex(cm => cm.ChatId);

            // Relationship configuration
            // Note: Changed to WithMany() assuming Chat can have multiple mentions
            // If it's truly one-to-one, keep WithOne()
            builder
                .HasOne(cm => cm.Chat)
                .WithOne(c => c.Mention) // or WithOne(c => c.Mention) if one-to-one
                .HasForeignKey<ChatMention>(cm => cm.ChatId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
