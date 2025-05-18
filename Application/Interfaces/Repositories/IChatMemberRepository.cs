using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Repositories
{
    public interface IChatMemberRepository
    {
        public Task<ChatMember> AddAsync(ChatMember member, CancellationToken cancellationToken = default);
        public Task<ChatMember> UpdateAsync(ChatMember member, CancellationToken cancellationToken = default);
        public Task DeleteAsync(ChatMember member, CancellationToken cancellationToken = default);
        public Task<ChatMember?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        public Task<ChatMember?> GetByIdsAsync(Guid chatId, Guid userId, CancellationToken cancellationToken = default);
        public Task<List<ChatMember>?> GetByChatIdAsync(Guid chatId, CancellationToken cancellationToken = default);
        public Task<List<ChatMember>?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
