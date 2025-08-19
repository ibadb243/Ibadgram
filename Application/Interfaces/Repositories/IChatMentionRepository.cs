using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Repositories
{
    public interface IChatMentionRepository
    {
        public Task<ChatMention> AddAsync(ChatMention chatMention, CancellationToken cancellationToken = default);
        public Task<ChatMention> UpdateAsync(ChatMention chatMention, CancellationToken cancellationToken = default);
        public Task DeleteAsync(ChatMention mention, CancellationToken cancellationToken = default);
        public Task<ChatMention?> GetByShortnameAsync(string shortname, CancellationToken cancellationToken = default);
        public Task<ChatMention?> GetByChatIdAsync(Guid chatId, CancellationToken cancellationToken = default);
    }
}
