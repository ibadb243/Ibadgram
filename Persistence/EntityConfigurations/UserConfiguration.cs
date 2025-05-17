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
            builder
                .HasKey(x => x.Id);

            builder
                .Property(x => x.Id)
                .ValueGeneratedOnAdd();

            builder
                .Property(x => x.Fullname)
                .HasMaxLength(256);

            builder
                .HasIndex(x => x.Email)
                .IsUnique();

            builder
                .HasMany(u => u.Memberships)
                .WithOne(m => m.User)
                .HasForeignKey(m => m.UserId);

            builder
                .HasMany(u => u.Messages)
                .WithOne(m => m.User)
                .HasForeignKey(m => m.UserId);
        }
    }
}
