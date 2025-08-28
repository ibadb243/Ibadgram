using Domain.Common;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;

namespace Infrastructure.Repositories
{
    /// <summary>
    /// Repository for managing mentions (user references by shortname) with operations for
    /// checking availability, searching, and retrieving mentions.
    /// </summary>
    public class MentionRepository : BaseRepository<Mention>, IMentionRepository
    {
        public MentionRepository(ApplicationDbContext context) : base(context) { }

        /// <summary>
        /// Checks if a mention with the specified shortname exists in the database.
        /// </summary>
        /// <param name="shortname">The shortname to check for existence.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>True if mention exists; otherwise false.</returns>
        public async Task<bool> ExistsByShortnameAsync(string shortname, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(shortname))
                return false;

            return await GetFilteredQuery()
                .AnyAsync(m => m.Shortname == shortname.ToLowerInvariant(), cancellationToken);
        }

        /// <summary>
        /// Retrieves a mention by its shortname.
        /// </summary>
        /// <param name="shortname">The shortname to search for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>The mention if found; otherwise null.</returns>
        public async Task<Mention?> GetByShortnameAsync(string shortname, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(shortname))
                return null;

            return await GetFilteredQuery()
                .FirstOrDefaultAsync(m => m.Shortname == shortname.ToLowerInvariant(), cancellationToken);
        }

        /// <summary>
        /// Checks if a shortname is available for use (not already taken by another mention).
        /// Note: The excludeId parameter is currently not implemented as intended.
        /// </summary>
        /// <param name="shortname">The shortname to check for availability.</param>
        /// <param name="excludeId">Optional ID to exclude from the check (for updating existing mentions).</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>True if shortname is available; otherwise false.</returns>
        public async Task<bool> IsShortnameAvailableAsync(string shortname, Guid? excludeId = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(shortname))
                return false;

            return await GetFilteredQuery()
                .AnyAsync(m => m.Shortname == shortname.ToLowerInvariant(), cancellationToken);
        }

        /// <summary>
        /// Searches for mentions by shortname using a search term.
        /// </summary>
        /// <param name="searchTerm">The term to search for in shortnames.</param>
        /// <param name="limit">Maximum number of results to return (capped at 50).</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>Collection of mentions matching the search criteria.</returns>
        public async Task<IEnumerable<Mention>> SearchByShortnameAsync(string searchTerm, int limit = 10, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Enumerable.Empty<Mention>();

            if (limit > 50) limit = 50;

            var term = searchTerm.Trim().ToLowerInvariant();
            return await GetFilteredQuery()
                .Where(m => m.Shortname.ToLower().Contains(term))
                .Take(limit) // Limit results
                .ToListAsync(cancellationToken);
        }
    }
}
