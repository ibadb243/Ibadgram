using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Repositories
{
    public interface IMentionRepository
    {
        /* base CRUD operations */
        Task<Mention?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Mention?> GetByShortnameAsync(string shortname, CancellationToken cancellationToken = default);
        Task<Mention> AddAsync(Mention mention, CancellationToken cancellationToken = default);
        Task<Mention> UpdateAsync(Mention mention, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /* specific methods */
        Task<IEnumerable<Mention>> SearchByShortnameAsync(string searchTerm, CancellationToken cancellationToken = default);
        Task<bool> ExistsByShortnameAsync(string shortname, CancellationToken cancellationToken = default);
    }
}
