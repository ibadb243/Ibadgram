using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Repositories
{
    public interface IUserMentionRepository : IBaseRepository<UserMention>
    {
        Task<UserMention?> GetByShortnameAsync(string shortname, CancellationToken cancellationToken = default);
        Task<UserMention?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> ShortnameExistsAsync(string shortname, CancellationToken cancellationToken = default);
    }
}
