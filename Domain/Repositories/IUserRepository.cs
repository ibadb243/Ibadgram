using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Repositories
{
    public interface IUserRepository : IBaseRepository<User>
    {
        Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
        Task<User?> GetWithMembershipsAsync(Guid id, CancellationToken cancellationToken = default);
        Task<User?> GetWithRefreshTokensAsync(Guid id, CancellationToken cancellationToken = default);
        Task<User?> GetByEmailWithPasswordAsync(string email, string passwordHash, CancellationToken cancellationToken = default);
        Task ConfirmEmailAsync(Guid userId, CancellationToken cancellationToken = default);
        Task UpdatePasswordAsync(Guid userId, string passwordHash, CancellationToken cancellationToken = default);
        Task<IEnumerable<User>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default);
        Task<IEnumerable<User>> GetVerifiedUsersAsync(CancellationToken cancellationToken = default);
        Task<(IEnumerable<User> Users, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
        Task<int> GetVerifiedUsersCountAsync(CancellationToken cancellationToken = default);
    }
}
