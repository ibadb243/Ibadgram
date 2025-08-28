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
    /// Repository for managing chat mentions (shortname-based chat references).
    /// Provides operations for retrieving and checking chat mentions by shortname or chat ID.
    /// </summary>
    public class ChatMentionRepository : BaseRepository<ChatMention>, IChatMentionRepository
    {
        public ChatMentionRepository(ApplicationDbContext context) : base(context) { }

        /// <summary>
        /// Retrieves a chat mention by its shortname.
        /// </summary>
        /// <param name="shortname">The shortname to search for (case-insensitive).</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>The chat mention if found; otherwise null.</returns>
        public async Task<ChatMention?> GetByShortnameAsync(
            string shortname,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(shortname))
            {
                return null;
            }

            return await GetFilteredQuery()
                .FirstOrDefaultAsync(
                    cm => cm.Shortname == shortname.ToLowerInvariant(),
                    cancellationToken);
        }

        /// <summary>
        /// Retrieves a chat mention by the associated chat ID.
        /// </summary>
        /// <param name="chatId">The ID of the chat to retrieve mention for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>The chat mention if found; otherwise null.</returns>
        public async Task<ChatMention?> GetByChatIdAsync(
            Guid chatId,
            CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .FirstOrDefaultAsync(
                    cm => cm.ChatId == chatId,
                    cancellationToken);
        }

        /// <summary>
        /// Checks if a shortname is already in use by any chat mention.
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
                    cm => cm.Shortname == shortname.ToLowerInvariant(),
                    cancellationToken);
        }
    }
}
