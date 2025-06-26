using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Repositories
{
    public interface IRefreshTokenRepository
    {
        /* base CRUD opearions */
        Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
        Task<RefreshToken> AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
        Task<RefreshToken> UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

        /* for users */
        Task<IEnumerable<RefreshToken>> GetUserTokensAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IEnumerable<RefreshToken>> GetActiveUserTokensAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<RefreshToken?> GetActiveTokenByValueAsync(string token, CancellationToken cancellationToken = default);

        /* manage tokens */
        Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default);
        Task RevokeTokenAsync(Guid tokenId, CancellationToken cancellationToken = default);
        Task RevokeAllUserTokensAsync(Guid userId, CancellationToken cancellationToken = default);
        Task RevokeExpiredTokensAsync(CancellationToken cancellationToken = default);

        /* validation and check */
        Task<bool> IsTokenValidAsync(string token, CancellationToken cancellationToken = default);
        Task<bool> TokenExistsAsync(string token, CancellationToken cancellationToken = default);

        /* cleanup  */
        Task CleanupExpiredTokensAsync(CancellationToken cancellationToken = default);
        Task CleanupRevokedTokensAsync(DateTime olderThan, CancellationToken cancellationToken = default);

        /* stats */
        Task<int> GetActiveTokensCountAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<int> GetTotalActiveTokensCountAsync(CancellationToken cancellationToken = default);
        Task<DateTime?> GetLastTokenCreationTimeAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
