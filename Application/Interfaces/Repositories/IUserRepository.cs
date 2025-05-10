using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Repositories
{
    public interface IUserRepository
    {
        public Task<User> AddAsync(User user, CancellationToken cancellationToken = default);
        public Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);
        public Task<uint> DeleteAsync(User user, CancellationToken cancellationToken = default);
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        public Task<User?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    }
}
