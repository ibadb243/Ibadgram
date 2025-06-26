using Application.Interfaces.Repositories;
using Domain.Common;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<MentionRepository> _logger;

        public MentionRepository(
            ApplicationDbContext context,
            ILogger<MentionRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<Mention?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Mentions.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        }

        public async Task<Mention?> GetByShortnameAsync(string shortname, CancellationToken cancellationToken = default)
        {
            return await _context.Mentions.FirstOrDefaultAsync(m => m.Shortname == shortname, cancellationToken);
        }

        public async Task<Mention> AddAsync(Mention mention, CancellationToken cancellationToken = default)
        {
            await _context.Mentions.AddAsync(mention, cancellationToken);

            _logger.LogInformation("Mention created with ID: {MentionId}", mention.Id);
            return mention;
        }

        public async Task<Mention> UpdateAsync(Mention mention, CancellationToken cancellationToken = default)
        {
            _context.Mentions.Update(mention);

            _logger.LogInformation("Mention updated with ID: {MentionId}", mention.Id);
            return mention;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var mention = await GetByIdAsync(id, cancellationToken);
            if (mention == null)
            {
                throw new Exception($"Mention with ID {id} not found");
            }

            _context.Mentions.Remove(mention);

            _logger.LogInformation("Mention deleted: {MentionId}", id);
        }

        public async Task<IEnumerable<Mention>> SearchByShortnameAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            return await _context.Mentions
                .Where(m => m.Shortname.Contains(searchTerm))
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ExistsByShortnameAsync(string shortname, CancellationToken cancellationToken = default)
        {
            return await _context.Mentions.AnyAsync(m => m.Shortname == shortname, cancellationToken);
        }
    }
}
