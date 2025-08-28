using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Repositories
{
    public interface IMessageRepository : IBaseRepository<Message>
    {
        Task<Message?> GetByCompositeIdAsync(Guid chatId, long messageId, CancellationToken cancellationToken = default);
        Task<IEnumerable<Message>> GetChatMessagesAsync(Guid chatId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
        Task<IEnumerable<Message>> GetUserMessagesAsync(Guid userId, int limit = 50, int offset = 0, CancellationToken cancellationToken = default);
        Task<long> GetNextMessageIdAsync(Guid chatId, CancellationToken cancellationToken = default);
        Task<int> GetChatMessageCountAsync(Guid chatId, CancellationToken cancellationToken = default);
        Task<Message?> GetLastMessageAsync(Guid chatId, CancellationToken cancellationToken = default);
        IQueryable<Message> GetQueryable();
    }
}
