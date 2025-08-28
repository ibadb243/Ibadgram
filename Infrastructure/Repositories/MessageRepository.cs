using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;

namespace Infrastructure.Repositories
{
    /// <summary>
    /// Repository for managing chat messages with operations for retrieving, counting, 
    /// and paginating messages within chats and for users.
    /// Handles message persistence with proper relationships to users and chats.
    /// </summary>
    public class MessageRepository : BaseRepository<Message>, IMessageRepository
    {
        public MessageRepository(ApplicationDbContext context) : base(context) { }

        /// <summary>
        /// Retrieves a message using its composite identifier (chat ID + message ID).
        /// Message ID is unique within a chat but not globally, hence requiring both identifiers.
        /// Includes related User and Chat entities for full message context.
        /// </summary>
        /// <param name="chatId">The ID of the chat containing the message.</param>
        /// <param name="messageId">The ID of the message within the chat (sequential number).</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>The message if found; otherwise null.</returns>
        public async Task<Message?> GetByCompositeIdAsync(Guid chatId, long messageId, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Include(m => m.User)
                .Include(m => m.Chat)
                .FirstOrDefaultAsync(m => m.ChatId == chatId && m.Id == messageId, cancellationToken);
        }

        /// <summary>
        /// Retrieves a paginated list of messages for a specific chat.
        /// Messages are ordered from newest to oldest (descending by creation time).
        /// Includes related User entities for message authors.
        /// </summary>
        /// <param name="chatId">The ID of the chat to retrieve messages for.</param>
        /// <param name="limit">Maximum number of messages to return (capped at 100).</param>
        /// <param name="offset">Number of messages to skip for pagination.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>Paginated collection of messages for the chat.</returns>
        /// <remarks>
        /// Note: 
        /// - Maximum limit is enforced at 100 messages to prevent excessive data transfer
        /// - Negative offset values are normalized to 0
        /// - Messages are ordered by creation time descending (newest first)
        /// </remarks>
        public async Task<IEnumerable<Message>> GetChatMessagesAsync(Guid chatId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        {
            if (limit > 100) limit = 100;
            if (offset < 0) offset = 0;

            return await GetFilteredQuery()
                .Include(m => m.User)
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.CreatedAtUtc)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Retrieves a paginated list of messages sent by a specific user.
        /// Messages are ordered from newest to oldest (descending by creation time).
        /// Includes related Chat entities for message context.
        /// </summary>
        /// <param name="userId">The ID of the user who sent the messages.</param>
        /// <param name="limit">Maximum number of messages to return (capped at 100).</param>
        /// <param name="offset">Number of messages to skip for pagination.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>Paginated collection of messages sent by the user.</returns>
        /// <remarks>
        /// Note: 
        /// - Maximum limit is enforced at 100 messages to prevent excessive data transfer
        /// - Negative offset values are normalized to 0
        /// - Messages are ordered by creation time descending (newest first)
        /// </remarks>
        public async Task<IEnumerable<Message>> GetUserMessagesAsync(Guid userId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default)
        {
            if (limit > 100) limit = 100;
            if (offset < 0) offset = 0;

            return await GetFilteredQuery()
                .Include(m => m.Chat)
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.CreatedAtUtc)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Generates the next sequential message ID for a chat.
        /// Message IDs are sequential within each chat (1, 2, 3, ...) but not globally unique.
        /// </summary>
        /// <param name="chatId">The ID of the chat to generate next message ID for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>The next available message ID for the chat.</returns>
        /// <remarks>
        /// Note: This implementation uses a simple increment approach.
        /// For high-concurrency scenarios, consider using database sequences or other 
        /// mechanisms to prevent ID collisions under heavy load.
        /// </remarks>
        public async Task<long> GetNextMessageIdAsync(Guid chatId, CancellationToken cancellationToken = default)
        {
            var lastMessage = await GetFilteredQuery()
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.Id)
                .FirstOrDefaultAsync(cancellationToken);

            return lastMessage?.Id + 1 ?? 1;
        }

        /// <summary>
        /// Counts the total number of messages in a specific chat.
        /// </summary>
        /// <param name="chatId">The ID of the chat to count messages for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>Total number of messages in the chat.</returns>
        public async Task<int> GetChatMessageCountAsync(Guid chatId, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .CountAsync(m => m.ChatId == chatId, cancellationToken);
        }

        /// <summary>
        /// Retrieves the most recent message in a chat.
        /// Includes related User entity for author information.
        /// </summary>
        /// <param name="chatId">The ID of the chat to retrieve the last message from.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>The most recent message if chat has messages; otherwise null.</returns>
        public async Task<Message?> GetLastMessageAsync(Guid chatId, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Include(m => m.User)
                .Where(m => m.ChatId == chatId)
                .OrderByDescending(m => m.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        /// <summary>
        /// Provides direct access to the underlying message query.
        /// Use this method for advanced querying scenarios that require custom filtering or projection.
        /// </summary>
        /// <returns>IQueryable for building custom message queries.</returns>
        /// <remarks>
        /// Note: The returned query already applies global filters (like soft delete) 
        /// from the base repository. Use with caution as improper usage could lead to 
        /// performance issues or unintended data exposure.
        /// </remarks>
        public IQueryable<Message> GetQueryable()
        {
            return GetFilteredQuery();
        }
    }
}
