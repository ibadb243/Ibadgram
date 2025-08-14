using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Repositories
{
    public interface IUserMentionRepository
    {
        public Task<UserMention> AddAsync(UserMention userMention, CancellationToken cancellationToken = default);
        public Task<UserMention> UpdateAsync(UserMention userMention, CancellationToken cancellationToken = default);
        public Task DeleteAsync(UserMention mention, CancellationToken cancellationToken = default);
        public Task<UserMention?> GetByShortnameAsync(string shortname, CancellationToken cancellationToken = default);
        public Task<UserMention?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
