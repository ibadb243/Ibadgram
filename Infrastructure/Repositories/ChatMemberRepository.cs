using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;

namespace Infrastructure.Repositories
{
    /// <summary>
    /// Repository for managing chat membership relationships between users and chats.
    /// Provides operations for adding, removing, and querying chat members.
    /// </summary>
    public class ChatMemberRepository : BaseRepository<ChatMember>, IChatMemberRepository
    {
        public ChatMemberRepository(ApplicationDbContext context) : base(context) { }

        /// <summary>
        /// Retrieves a specific chat membership by chat ID and user ID.
        /// Includes related User and Chat entities for full context.
        /// </summary>
        /// <param name="chatId">The ID of the chat to check membership for.</param>
        /// <param name="userId">The ID of the user to check membership for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>The chat membership if found; otherwise null.</returns>
        public async Task<ChatMember?> GetByIdsAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Include(m => m.User)
                .Include(m => m.Chat)
                .FirstOrDefaultAsync(m => m.ChatId == chatId && m.UserId == userId, cancellationToken);
        }

        /// <summary>
        /// Retrieves all members of a specific chat.
        /// Includes related User entities for member details.
        /// </summary>
        /// <param name="chatId">The ID of the chat to retrieve members for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>Collection of chat members with associated user details.</returns>
        public async Task<IEnumerable<ChatMember>> GetByChatIdAsync(Guid chatId, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Include(m => m.User)
                .Where(m => m.ChatId == chatId)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Retrieves all chats a specific user is a member of.
        /// Includes related Chat entities for chat details.
        /// </summary>
        /// <param name="userId">The ID of the user to retrieve chats for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>Collection of chat memberships with associated chat details.</returns>
        public async Task<IEnumerable<ChatMember>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Include(m => m.Chat)
                .Where(m => m.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Checks if a user is a member of a specific chat.
        /// </summary>
        /// <param name="chatId">The ID of the chat to check.</param>
        /// <param name="userId">The ID of the user to check.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>True if user is a member of the chat; otherwise false.</returns>
        public async Task<bool> IsMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .AnyAsync(m => m.ChatId == chatId && m.UserId == userId, cancellationToken);
        }

        /// <summary>
        /// Gets the total number of members in a specific chat.
        /// </summary>
        /// <param name="chatId">The ID of the chat to count members for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>Number of members in the chat.</returns>
        public async Task<int> GetMemberCountAsync(Guid chatId, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .CountAsync(m => m.ChatId == chatId, cancellationToken);
        }

        /// <summary>
        /// Removes a user from a chat by deleting their membership record.
        /// First retrieves the membership to ensure it exists before deletion.
        /// </summary>
        /// <param name="chatId">The ID of the chat to remove membership from.</param>
        /// <param name="userId">The ID of the user to remove from the chat.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <remarks>
        /// Note: This implementation first fetches the entity before deletion.
        /// For high-performance scenarios, consider using direct delete query:
        /// Context.ChatMembers.Where(m => m.ChatId == chatId && m.UserId == userId).ExecuteDeleteAsync()
        /// </remarks>
        public async Task RemoveMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default)
        {
            var member = await GetByIdsAsync(chatId, userId, cancellationToken);
            if (member != null)
            {
                await DeleteAsync(member, cancellationToken);
            }
        }
    }
}
