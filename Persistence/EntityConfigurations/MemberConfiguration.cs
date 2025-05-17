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
    public class MemberConfiguration : IEntityTypeConfiguration<ChatMember>
    {
        public void Configure(EntityTypeBuilder<ChatMember> builder)
        {
            builder
                .HasKey(x => x.Id);

            builder
                .Property(x => x.Id)
                .ValueGeneratedOnAdd();

            builder
                .HasIndex(x => new { x.ChatId, x.UserId })
                .IsUnique();

            builder
                .Property(x => x.ChatId)
                .IsRequired();

            builder
                .Property(x => x.UserId)
                .IsRequired();

            builder
                .Property(x => x.Nickname)
                .HasMaxLength(64);
        }
    }
}
