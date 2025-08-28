using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Repositories
{
    public interface IRefreshTokenRepository : IBaseRepository<RefreshToken>
    {
        Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
        Task<IEnumerable<RefreshToken>> GetUserTokensAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<RefreshToken>> GetActiveUserTokensAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<RefreshToken?> GetActiveTokenByValueAsync(string token, CancellationToken cancellationToken = default);
        Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default);
        Task RevokeAllUserTokensAsync(Guid userId, CancellationToken cancellationToken = default);
        Task RevokeExpiredTokensAsync(CancellationToken cancellationToken = default);
        Task<bool> IsTokenValidAsync(string token, CancellationToken cancellationToken = default);
        Task<bool> TokenExistsAsync(string token, CancellationToken cancellationToken = default);
        Task CleanupExpiredTokensAsync(CancellationToken cancellationToken = default);
        Task CleanupRevokedTokensAsync(DateTime olderThan, CancellationToken cancellationToken = default);
        Task<int> GetActiveTokensCountAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<int> GetTotalActiveTokensCountAsync(CancellationToken cancellationToken = default);
        Task<DateTime?> GetLastTokenCreationTimeAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
