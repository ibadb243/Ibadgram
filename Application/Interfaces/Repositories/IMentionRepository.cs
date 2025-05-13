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
        public Task<Mention> AddAsync(Mention mention, CancellationToken cancellationToken = default);
        public Task<Mention> UpdateAsync(Mention mention, CancellationToken cancellationToken = default);
        public Task<uint> DeleteAsync(Mention mention, CancellationToken cancellationToken = default);
        public Task<Mention?> GetByShortnameAsync(string shortname, CancellationToken cancellationToken = default);
    }
}
