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
    public class ChatMentionConfiguration : IEntityTypeConfiguration<ChatMention>
    {
        public void Configure(EntityTypeBuilder<ChatMention> builder)
        {
            builder
                .HasIndex(cm => cm.ChatId);

            builder
                .HasOne(cm => cm.Chat)
                .WithOne(c => c.Mention)
                .HasForeignKey<ChatMention>(cm => cm.ChatId)
                .IsRequired(false);
        }
    }
}
