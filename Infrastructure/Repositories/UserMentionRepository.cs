using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    /// <summary>
    /// Repository for managing user mentions (shortname-based user references).
    /// Provides operations for retrieving and checking user mentions by shortname or user ID.
    /// </summary>
    public class UserMentionRepository : BaseRepository<UserMention>, IUserMentionRepository
    {
        public UserMentionRepository(ApplicationDbContext context)
            : base(context) { }

        /// <summary>
        /// Retrieves a user mention by its shortname.
        /// </summary>
        /// <param name="shortname">The shortname to search for (case-insensitive).</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>The user mention if found; otherwise null.</returns>
        public async Task<UserMention?> GetByShortnameAsync(
            string shortname,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(shortname))
            {
                return null;
            }

            return await GetFilteredQuery()
                .FirstOrDefaultAsync(
                    um => um.Shortname == shortname.ToLowerInvariant(),
                    cancellationToken);
        }

        /// <summary>
        /// Retrieves a user mention by the associated user ID.
        /// </summary>
        /// <param name="userId">The ID of the user to retrieve mention for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>The user mention if found; otherwise null.</returns>
        public async Task<UserMention?> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .FirstOrDefaultAsync(
                    um => um.UserId == userId,
                    cancellationToken);
        }

        /// <summary>
        /// Checks if a shortname is already in use by any user mention.
        /// </summary>
        /// <param name="shortname">The shortname to check for existence.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>True if shortname exists; otherwise false.</returns>
        public async Task<bool> ShortnameExistsAsync(
            string shortname,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(shortname))
            {
                return false;
            }

            return await GetFilteredQuery()
                .AnyAsync(
                    um => um.Shortname == shortname.ToLowerInvariant(),
                    cancellationToken);
        }
    }
}
