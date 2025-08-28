using Application.Interfaces.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class MessageRepository : BaseRepository<Message>, IMessageRepository
    {
        public MessageRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Message?> GetByCompositeIdAsync(Guid chatId, long messageId, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Include(m => m.User)
                .Include(m => m.Chat)
                .FirstOrDefaultAsync(m => m.ChatId == chatId && m.Id == messageId, cancellationToken);
        }

        public async Task<IEnumerable<Message>> GetChatMessagesAsync(Guid chatId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        {
            if (limit > 100) limit = 100;
            if (offset < 0) offset = 0;

            return await GetFilteredQuery()
                .Include(m => m.User)
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.CreatedAtUtc)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<Message>> GetUserMessagesAsync(Guid userId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        {
            if (limit > 100) limit = 100;
            if (offset < 0) offset = 0;

            return await GetFilteredQuery()
                .Include(m => m.Chat)
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.CreatedAtUtc)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<long> GetNextMessageIdAsync(Guid chatId, CancellationToken cancellationToken = default)
        {
            var lastMessage = await GetFilteredQuery()
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return lastMessage?.Id + 1 ?? 1;
        }

        public async Task<int> GetChatMessageCountAsync(Guid chatId, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .CountAsync(m => m.ChatId == chatId, cancellationToken);
        }

        public async Task<Message?> GetLastMessageAsync(Guid chatId, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Include(m => m.User)
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public IQueryable<Message> GetQueryable()
        {
            return GetFilteredQuery();
        }
    }
}
