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
    public class MessageRepository : IMessageRepository
    {
        private readonly ApplicationDbContext _context;

        public MessageRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Message> AddAsync(Message message, CancellationToken cancellationToken = default)
        {
            await _context.Messages.AddAsync(message, cancellationToken);
            return message;
        }

        public Task<Message> UpdateAsync(Message message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _context.Messages.Update(message);
            return Task.FromResult(message);
        }

        public Task DeleteAsync(Message message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _context.Messages.Remove(message);
            return Task.CompletedTask;
        }

        public async Task<Message> GetByIdAsync(Guid chatId, long messageId, CancellationToken cancellationToken = default)
        {
            return await _context.Messages.FirstOrDefaultAsync(m => m.ChatId == chatId && m.Id == messageId, cancellationToken);
        }
    }
}
