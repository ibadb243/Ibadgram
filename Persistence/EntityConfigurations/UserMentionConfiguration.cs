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
    public class UserMentionConfiguration : IEntityTypeConfiguration<UserMention>
    {
        public void Configure(EntityTypeBuilder<UserMention> builder)
        {
            builder
                .HasIndex(x => x.UserId);

            builder
                .HasOne(um => um.User)
                .WithOne(u => u.Mention)
                .HasForeignKey<UserMention>(um => um.UserId)
                .IsRequired(false);
        }
    }
}
