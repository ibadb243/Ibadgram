using Domain.Common;
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
    public class MentionConfiguration : IEntityTypeConfiguration<Mention>
    {
        public void Configure(EntityTypeBuilder<Mention> builder)
        {
            // Table name
            builder.ToTable("Mentions");

            // Configure Table-Per-Hierarchy (TPH) inheritance
            builder
                .HasDiscriminator<string>("MentionType")
                .HasValue<ChatMention>("Chat")
                .HasValue<UserMention>("User");

            // Primary key
            builder
                .HasKey(x => x.Id);

            builder
                .Property(x => x.Id)
                .ValueGeneratedOnAdd();

            // Shortname configuration
            builder
                .Property(x => x.Shortname)
                .IsRequired()
                .HasMaxLength(ShortnameConstants.MaxLength);

            // Unique constraint on shortname
            builder
                .HasIndex(x => x.Shortname)
                .IsUnique();
        }
    }
}
