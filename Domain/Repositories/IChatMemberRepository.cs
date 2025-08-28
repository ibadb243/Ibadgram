using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Repositories
{
    public interface IChatMemberRepository : IBaseRepository<ChatMember>
    {
        Task<ChatMember?> GetByIdsAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<ChatMember>> GetByChatIdAsync(Guid chatId, CancellationToken cancellationToken = default);
        Task<IEnumerable<ChatMember>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> IsMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);
        Task<int> GetMemberCountAsync(Guid chatId, CancellationToken cancellationToken = default);
        Task RemoveMemberAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);
    }
}
