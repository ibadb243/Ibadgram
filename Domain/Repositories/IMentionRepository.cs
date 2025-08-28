using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Repositories
{
    public interface IMentionRepository : IBaseRepository<Mention>
    {
        Task<Mention?> GetByShortnameAsync(string shortname, CancellationToken cancellationToken = default);
        Task<IEnumerable<Mention>> SearchByShortnameAsync(string searchTerm, int limit = 10, CancellationToken cancellationToken = default);
        Task<bool> ExistsByShortnameAsync(string shortname, CancellationToken cancellationToken = default);
        Task<bool> IsShortnameAvailableAsync(string shortname, Guid? excludeId = null, CancellationToken cancellationToken = default);
    }
}
