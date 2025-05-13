using Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Repositories
{
    public interface IChatRepository
    {
        public Task<BaseChat> AddAsync(BaseChat chat, CancellationToken cancellationToken = default);
        public Task<BaseChat> UpdateAsync(BaseChat chat, CancellationToken cancellationToken = default);
        public Task<uint> DeleteAsync(BaseChat chat, CancellationToken cancellationToken = default);
        public Task<BaseChat?> GetByIdAsync(Guid Id, CancellationToken cancellationToken = default);
        public Task<IEnumerable<BaseChat>> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    }
}
