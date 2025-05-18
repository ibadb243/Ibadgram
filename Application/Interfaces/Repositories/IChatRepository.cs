using Domain.Common;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Repositories
{
    public interface IChatRepository
    {
        public Task<Chat> AddAsync(Chat chat, CancellationToken cancellationToken = default);
        public Task<Chat> UpdateAsync(Chat chat, CancellationToken cancellationToken = default);
        public Task DeleteAsync(Chat chat, CancellationToken cancellationToken = default);
        public Task<Chat?> GetByIdAsync(Guid Id, CancellationToken cancellationToken = default);
        public Task<List<Chat>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        public Task<Chat?> FindOneToOneChatAsync(Guid userId1, Guid userId2, CancellationToken cancellationToken = default);
    }
}
