using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Repositories
{
    public interface IMessageRepository
    {
        public Task<Message> AddAsync(Message message, CancellationToken cancellationToken = default);
        public Task<Message> UpdateAsync(Message message, CancellationToken cancellationToken = default);
        public Task DeleteAsync(Message message, CancellationToken cancellationToken = default);
        public Task<Message> GetByIdAsync(Guid chatId, long messageId, CancellationToken cancellationToken = default);
    }
}
