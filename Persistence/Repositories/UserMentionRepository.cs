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
    public class UserMentionRepository : IUserMentionRepository
    {
        private readonly ApplicationDbContext _context;

        public UserMentionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<UserMention> AddAsync(UserMention userMention, CancellationToken cancellationToken = default)
        {
            await _context.UserMentions.AddAsync(userMention, cancellationToken);
            return userMention;
        }

        public Task<UserMention> UpdateAsync(UserMention userMention, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _context.UserMentions.Update(userMention);
            return Task.FromResult(userMention);
        }

        public Task DeleteAsync(UserMention mention, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _context.UserMentions.Remove(mention);
            return Task.CompletedTask;
        }

        public async Task<UserMention?> GetByShortnameAsync(string shortname, CancellationToken cancellationToken = default)
        {
            return await _context.UserMentions.FirstOrDefaultAsync(m => m.Shortname == shortname, cancellationToken);
        }
    }
}
