using Application.Interfaces.Repositories;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Persistence.Repositories
{
    public class ChatRepository : IChatRepository
    {
        private readonly ApplicationDbContext _context;

        public ChatRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Chat> AddAsync(Chat chat, CancellationToken cancellationToken = default)
        {
            await _context.Chats.AddAsync(chat, cancellationToken);
            return chat;
        }

        public Task<Chat> UpdateAsync(Chat chat, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entity = _context.Chats.Update(chat);
            return Task.FromResult(entity.Entity);
        }

        public Task DeleteAsync(Chat chat, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _context.Chats.Remove(chat);
            return Task.CompletedTask;
        }

        public async Task<Chat?> GetByIdAsync(Guid Id, CancellationToken cancellationToken = default)
        {
            return await _context.Chats
                .Include(c => c.Mention)
                .Include(c => c.Members)
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(x => x.Id == Id, cancellationToken);
        }

        public async Task<List<Chat>> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            return await _context.Chats
                .Where(c => EF.Functions.Like(c.Name, $"%{name}%"))
                .Take(3)
                .ToListAsync();
        }

        public async Task<Chat?> FindOneToOneChatAsync(Guid userId1, Guid userId2, CancellationToken cancellationToken = default)
        {
            return await _context.Chats
                .Include(c => c.Members)
                .Include(c => c.Messages)
                .Where(c => c.Type == ChatType.OneToOne)
                .Where(c => 
                    (c.Members[0].UserId == userId1 && c.Members[1].UserId == userId2) ||
                    (c.Members[1].UserId == userId1 && c.Members[0].UserId == userId2))
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
