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
    public class ChatMentionRepository : IChatMentionRepository
    {
        private readonly ApplicationDbContext _context;

        public ChatMentionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ChatMention> AddAsync(ChatMention chatMention, CancellationToken cancellationToken = default)
        {
            await _context.ChatMentions.AddAsync(chatMention, cancellationToken);
            return chatMention;
        }

        public Task<ChatMention> UpdateAsync(ChatMention chatMention, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _context.ChatMentions.Update(chatMention);
            return Task.FromResult(chatMention);
        }

        public Task DeleteAsync(ChatMention mention, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _context.ChatMentions.Remove(mention);
            return Task.CompletedTask;
        }

        public async Task<ChatMention?> GetByShortnameAsync(string shortname, CancellationToken cancellationToken = default)
        {
            return await _context.ChatMentions.FirstOrDefaultAsync(m => m.Shortname == shortname, cancellationToken);
        }

        public async Task<ChatMention?> GetByChatIdAsync(Guid chatId, CancellationToken cancellationToken = default)
        {
            return await _context.ChatMentions.FirstOrDefaultAsync(um => um.ChatId == chatId, cancellationToken);
        }
    }
}
