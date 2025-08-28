using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Repositories
{
    public interface IChatMentionRepository : IBaseRepository<ChatMention>
    {
        Task<ChatMention?> GetByShortnameAsync(string shortname, CancellationToken cancellationToken = default);
        Task<ChatMention?> GetByChatIdAsync(Guid chatId, CancellationToken cancellationToken = default);
        Task<bool> ShortnameExistsAsync(string shortname, CancellationToken cancellationToken = default);
    }
}
