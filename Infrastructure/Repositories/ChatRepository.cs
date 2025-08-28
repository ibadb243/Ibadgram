using Domain.Entities;
using Domain.Repositories;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class ChatRepository : BaseRepository<Chat>, IChatRepository
    {
        public ChatRepository(ApplicationDbContext context) : base(context) { }

        public async Task<IEnumerable<Chat>> GetByNameAsync(string name, int limit = 10, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Enumerable.Empty<Chat>();

            if (limit > 50) limit = 50; // Reasonable limit

            return await GetFilteredQuery()
                .Where(c => EF.Functions.Like(c.Name, $"%{name.Trim()}%"))
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        public async Task<Chat?> FindOneToOneChatAsync(Guid userId1, Guid userId2, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Include(c => c.Members)
                .Where(c => c.Type == ChatType.OneToOne && c.Members.Count == 2)
                .FirstOrDefaultAsync(c => c.Members.Any(m => m.UserId == userId1) &&
                                         c.Members.Any(m => m.UserId == userId2), cancellationToken);
        }

        public async Task<Chat?> GetWithMembersAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Include(c => c.Members)
                    .ThenInclude(m => m.User)
                .Include(c => c.Mention)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<Chat?> GetWithMessagesAsync(Guid id, int messageLimit = 50, CancellationToken cancellationToken = default)
        {
            if (messageLimit > 100) messageLimit = 100; // Reasonable limit

            return await GetFilteredQuery()
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAtUtc).Take(messageLimit))
                    .ThenInclude(m => m.User)
                .Include(c => c.Mention)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<Chat?> GetWithMembersAndMessagesAsync(Guid id, int messageLimit = 50, CancellationToken cancellationToken = default)
        {
            if (messageLimit > 100) messageLimit = 100;

            return await GetFilteredQuery()
                .Include(c => c.Members)
                    .ThenInclude(m => m.User)
                .Include(c => c.Messages.OrderByDescending(m => m.CreatedAtUtc).Take(messageLimit))
                    .ThenInclude(m => m.User)
                .Include(c => c.Mention)
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        }

        public async Task<IEnumerable<Chat>> GetUserChatsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Include(c => c.Members)
                    .ThenInclude(m => m.User)
                .Include(c => c.Mention)
                .Where(c => c.Members.Any(m => m.UserId == userId))
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync(cancellationToken);
        }
    }
}
