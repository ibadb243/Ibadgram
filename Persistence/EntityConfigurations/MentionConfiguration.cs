using Domain.Common;
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
            builder
                .HasKey(x => x.Id);

            builder
                .Property(x => x.Id)
                .ValueGeneratedOnAdd();

            builder
                .HasDiscriminator<int>("MentionType")
                    .HasValue<UserMention>(1)
                    .HasValue<ChatMention>(2);

            builder
                .HasIndex(x => x.Shortname)
                .IsUnique();

            builder
                .Property(x => x.Shortname)
                .HasMaxLength(64)
                .IsRequired();
        }
    }
}
