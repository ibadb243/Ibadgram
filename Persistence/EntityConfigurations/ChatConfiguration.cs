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
    public class ChatConfiguration : IEntityTypeConfiguration<Chat>
    {
        public void Configure(EntityTypeBuilder<Chat> builder)
        {
            // Table name
            builder
                .ToTable("Chats");

            // Primary Key
            builder
                .HasKey(x => x.Id);

            // Id
            builder
                .Property(x => x.Id)
                .ValueGeneratedOnAdd();

            // Type (enum)
            builder
                .Property(x => x.Type)
                .IsRequired()
                .HasConversion<int>(); // Сохраняется как integer в БД

            // Name - используем правильные константы из ChatConstants
            builder
                .Property(x => x.Name)
                .IsRequired(false) // В модели nullable
                .HasMaxLength(ChatConstants.NameMaxLength);

            // Description - используем константу из ChatConstants
            builder
                .Property(x => x.Description)
                .IsRequired(false)
                .HasMaxLength(ChatConstants.DescriptionLength);

            // IsPrivate
            builder
                .Property(x => x.IsPrivate)
                .IsRequired(false); // nullable bool в модели

            // CreatedAtUtc
            builder
                .Property(x => x.CreatedAtUtc)
                .IsRequired();

            // IsDeleted (для soft delete)
            builder
                .Property(x => x.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            // Navigation property

            // ChatMention (один к одному, nullable)
            builder
                .HasOne(c => c.Mention)
                .WithOne()
                .HasForeignKey<ChatMention>(m => m.ChatId)
                .IsRequired(false);

            // ChatMember (один ко многим)
            builder
                .HasMany(c => c.Members)
                .WithOne(m => m.Chat)
                .HasForeignKey(m => m.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            // Message (один ко многим)
            builder
                .HasMany(c => c.Messages)
                .WithOne(m => m.Chat)
                .HasForeignKey(m => m.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            // Глобальный фильтр для soft delete
            builder
                .HasQueryFilter(c => !c.IsDeleted);
        }
    }
}
