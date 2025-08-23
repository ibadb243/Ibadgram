using Domain.Common.Constants;
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
    public class MessageConfiguration : IEntityTypeConfiguration<Message>
    {
        public void Configure(EntityTypeBuilder<Message> builder)
        {
            // Имя таблицы
            builder
                .ToTable("Messages");

            // Первичный ключ
            builder
                .HasKey(x => x.Id);

            // Свойство Id - использует long вместо Guid
            builder
                .Property(x => x.Id)
                .ValueGeneratedOnAdd()
                .UseIdentityColumn(); // Для автоинкремента long

            // ChatId
            builder
                .Property(x => x.ChatId)
                .IsRequired();

            // UserId
            builder
                .Property(x => x.UserId)
                .IsRequired();

            // Text - используем константу из MessageConstants
            builder
                .Property(x => x.Text)
                .IsRequired()
                .HasMaxLength(MessageConstants.MaxLength);

            // CreatedAtUtc
            builder
                .Property(x => x.CreatedAtUtc)
                .IsRequired();

            // UpdatedAtUtc
            builder
                .Property(x => x.UpdatedAtUtc)
                .IsRequired(false);

            // IsDeleted (для soft delete)
            builder
                .Property(x => x.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            // Индексы

            // Составной индекс для поиска сообщений в чате от пользователя
            builder
                .HasIndex(x => new { x.ChatId, x.UserId })
                .HasDatabaseName("IX_Messages_ChatId_UserId");

            // Индекс для поиска сообщений по чату (упорядочено по времени)
            builder
                .HasIndex(x => new { x.ChatId, x.CreatedAtUtc })
                .HasDatabaseName("IX_Messages_ChatId_CreatedAtUtc");

            // Индекс для поиска сообщений пользователя
            builder
                .HasIndex(x => x.UserId)
                .HasDatabaseName("IX_Messages_UserId");

            // Навигационные свойства

            // Chat (многие к одному)
            builder
                .HasOne(m => m.Chat)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ChatId)
                .OnDelete(DeleteBehavior.Cascade);

            // User (многие к одному)
            builder
                .HasOne(m => m.User)
                .WithMany() // В модели User нет навигационного свойства Messages
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict); // Не удаляем сообщения при удалении пользователя

            // Глобальный фильтр для soft delete
            builder
                .HasQueryFilter(m => !m.IsDeleted);
        }
    }
}
