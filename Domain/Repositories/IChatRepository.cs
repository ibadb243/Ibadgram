using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Repositories
{
    public interface IChatRepository : IBaseRepository<Chat>
    {
        Task<IEnumerable<Chat>> GetByNameAsync(string name, int limit = 10, CancellationToken cancellationToken = default);
        Task<Chat?> FindOneToOneChatAsync(Guid userId1, Guid userId2, CancellationToken cancellationToken = default);
        Task<Chat?> GetWithMembersAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Chat?> GetWithMessagesAsync(Guid id, int messageLimit = 50, CancellationToken cancellationToken = default);
        Task<Chat?> GetWithMembersAndMessagesAsync(Guid id, int messageLimit = 50, CancellationToken cancellationToken = default);
        Task<IEnumerable<Chat>> GetUserChatsAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
