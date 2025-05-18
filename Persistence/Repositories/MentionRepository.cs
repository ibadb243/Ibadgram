using Application.Interfaces.Repositories;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Persistence.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Persistence.Repositories
{
    public class MentionRepository : IMentionRepository
    {
        private readonly ApplicationDbContext _context;

        public MentionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Mention> AddAsync(Mention mention, CancellationToken cancellationToken = default)
        {
            await _context.Mentions.AddAsync(mention, cancellationToken);
            return mention;
        }

        public Task<Mention> UpdateAsync(Mention mention, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entity = _context.Mentions.Update(mention);
            return Task.FromResult(entity.Entity);
        }

        public Task DeleteAsync(Mention mention, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _context.Mentions.Remove(mention);
            return Task.CompletedTask;
        }

        public async Task<Mention?> GetByShortnameAsync(string shortname, CancellationToken cancellationToken = default)
        {
            return await _context.Mentions.FirstOrDefaultAsync(m => m.Shortname == shortname, cancellationToken);
        }
    }
}
