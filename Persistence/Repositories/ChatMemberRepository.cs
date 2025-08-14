using Application.Interfaces.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Persistence.Repositories
{
    public class ChatMemberRepository : IChatMemberRepository
    {
        private readonly ApplicationDbContext _context;

        public ChatMemberRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ChatMember> AddAsync(ChatMember member, CancellationToken cancellationToken = default)
        {
            await _context.Members.AddAsync(member, cancellationToken);
            return member;
        }

        public Task<ChatMember> UpdateAsync(ChatMember member, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entity = _context.Members.Update(member);
            return Task.FromResult(entity.Entity);
        }

        public Task DeleteAsync(ChatMember member, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _context.Members.Remove(member);
            return Task.CompletedTask;
        }

        public async Task<ChatMember?> GetByIdsAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.Members
                .Include(m => m.User)
                .Include(m => m.Chat)
                .FirstOrDefaultAsync(m => m.ChatId == chatId && m.UserId == userId, cancellationToken);
        }

        public async Task<List<ChatMember>?> GetByChatIdAsync(Guid chatId, CancellationToken cancellationToken = default)
        {
            return await _context.Members
                .Include(m => m.User)
                .Include(m => m.Chat)
                .Where(m => m.ChatId == chatId)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<ChatMember>?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.Members
                .Include(m => m.User)
                .Include(m => m.Chat)
                .Where(m => m.UserId == userId)
                .ToListAsync(cancellationToken);
        }
    }
}
