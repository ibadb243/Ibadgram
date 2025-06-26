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
        /* base CRUD opeations */
        Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<User> AddAsync(User user, CancellationToken cancellationToken = default);
        Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

        /* for registration */
        Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

        /* specific methods */
        Task<User?> GetWithMembershipsAsync(Guid id, CancellationToken cancellationToken = default);
        Task<User?> GetWithRefreshTokensAsync(Guid id, CancellationToken cancellationToken = default);

        /* for authentication */
        Task<User?> GetByEmailWithPasswordAsync(
            string email, 
            string passwordHash, 
            CancellationToken cancellationToken = default);
        Task ConfirmEmailAsync(Guid userId, CancellationToken cancellationToken = default);
        Task UpdatePasswordSaltAndPasswordHashAsync(
            Guid userId, 
            string passwordSalt, 
            string passwordHash, 
            CancellationToken cancellationToken = default);

        /* for search & filters */
        Task<IEnumerable<User>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default);
        Task<IEnumerable<User>> GetVerifiedUsersAsync(CancellationToken cancellationToken = default);

        /* pagination */
        Task<(IEnumerable<User> Users, int TotalCount)> GetPagedAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        /* stats */
        Task<int> GetTotalUsersCountAsync(CancellationToken cancellationToken = default);
        Task<int> GetVerifiedUsersCountAsync(CancellationToken cancellationToken = default);
    }
}
