using Domain.Entities;
using Domain.Repositories;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    /// <summary>
    /// Repository for managing refresh tokens with all token-related operations.
    /// </summary>
    public class RefreshTokenRepository : BaseRepository<RefreshToken>, IRefreshTokenRepository
    {
        public RefreshTokenRepository(ApplicationDbContext context) : base(context) { }

        /// <summary>
        /// Gets a refresh token by its value.
        /// </summary>
        /// <param name="token">The token value to search for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>The refresh token if found; otherwise null.</returns>
        public async Task<RefreshToken?> GetByTokenAsync(
            string token,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            return await GetFilteredQuery()
                .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);
        }

        /// <summary>
        /// Gets all refresh tokens for a specific user.
        /// </summary>
        /// <param name="userId">The user ID to retrieve tokens for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>Collection of refresh tokens for the user.</returns>
        public async Task<IEnumerable<RefreshToken>> GetUserTokensAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Where(rt => rt.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Gets only active (non-revoked and non-expired) refresh tokens for a user.
        /// </summary>
        /// <param name="userId">The user ID to retrieve active tokens for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>Collection of active refresh tokens.</returns>
        public async Task<IEnumerable<RefreshToken>> GetActiveUserTokensAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Where(rt => rt.UserId == userId &&
                    !rt.IsRevoked &&
                    rt.ExpiresAtUtc > DateTime.UtcNow)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// Gets an active refresh token by its value (non-revoked and non-expired).
        /// </summary>
        /// <param name="token">The token value to search for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>The active refresh token if found; otherwise null.</returns>
        public async Task<RefreshToken?> GetActiveTokenByValueAsync(
            string token,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            return await GetFilteredQuery()
                .FirstOrDefaultAsync(rt => rt.Token == token &&
                    !rt.IsRevoked &&
                    rt.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);
        }

        /// <summary>
        /// Revokes a specific refresh token.
        /// </summary>
        /// <param name="token">The token value to revoke.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public async Task RevokeTokenAsync(
            string token,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return;
            }

            var refreshToken = await GetByTokenAsync(token, cancellationToken);
            if (refreshToken != null && !refreshToken.IsRevoked)
            {
                refreshToken.IsRevoked = true;
                await UpdateAsync(refreshToken, cancellationToken);
            }
        }

        /// <summary>
        /// Revokes all refresh tokens for a specific user.
        /// </summary>
        /// <param name="userId">The user ID whose tokens should be revoked.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public async Task RevokeAllUserTokensAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var tokens = await GetFilteredQuery()
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var token in tokens)
            {
                token.IsRevoked = true;
                await UpdateAsync(token, cancellationToken);
            }
        }

        /// <summary>
        /// Revokes all expired refresh tokens (those past their expiration date).
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public async Task RevokeExpiredTokensAsync(
            CancellationToken cancellationToken = default)
        {
            var expiredTokens = await GetFilteredQuery()
                .Where(rt => rt.ExpiresAtUtc < DateTime.UtcNow && !rt.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var token in expiredTokens)
            {
                token.IsRevoked = true;
                await UpdateAsync(token, cancellationToken);
            }
        }

        /// <summary>
        /// Checks if a refresh token is valid (exists, not revoked, and not expired).
        /// </summary>
        /// <param name="token">The token value to validate.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>True if token is valid; otherwise false.</returns>
        public async Task<bool> IsTokenValidAsync(
            string token,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return await GetFilteredQuery()
                .AnyAsync(rt => rt.Token == token &&
                    !rt.IsRevoked &&
                    rt.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);
        }

        /// <summary>
        /// Checks if a refresh token exists in the database.
        /// </summary>
        /// <param name="token">The token value to check.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>True if token exists; otherwise false.</returns>
        public async Task<bool> TokenExistsAsync(
            string token,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            return await GetFilteredQuery()
                .AnyAsync(rt => rt.Token == token, cancellationToken);
        }

        /// <summary>
        /// Cleans up expired refresh tokens by physically removing them from the database.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public async Task CleanupExpiredTokensAsync(
            CancellationToken cancellationToken = default)
        {
            var expiredTokens = await GetFilteredQuery()
                .Where(rt => rt.ExpiresAtUtc < DateTime.UtcNow)
                .ToListAsync(cancellationToken);

            foreach (var token in expiredTokens)
            {
                await DeleteAsync(token, cancellationToken);
            }
        }

        /// <summary>
        /// Cleans up revoked tokens that were created before a specific date.
        /// </summary>
        /// <param name="olderThan">Tokens created before this date will be cleaned up.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        public async Task CleanupRevokedTokensAsync(
            DateTime olderThan,
            CancellationToken cancellationToken = default)
        {
            var revokedTokens = await GetFilteredQuery()
                .Where(rt => rt.IsRevoked && rt.CreatedAtUtc < olderThan)
                .ToListAsync(cancellationToken);

            foreach (var token in revokedTokens)
            {
                await DeleteAsync(token, cancellationToken);
            }
        }

        /// <summary>
        /// Gets the count of active (non-revoked and non-expired) tokens for a user.
        /// </summary>
        /// <param name="userId">The user ID to count tokens for.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>Number of active tokens.</returns>
        public async Task<int> GetActiveTokensCountAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .CountAsync(rt => rt.UserId == userId &&
                    !rt.IsRevoked &&
                    rt.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);
        }

        /// <summary>
        /// Gets the total count of active (non-revoked and non-expired) tokens in the system.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>Total number of active tokens.</returns>
        public async Task<int> GetTotalActiveTokensCountAsync(
            CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .CountAsync(rt => !rt.IsRevoked &&
                    rt.ExpiresAtUtc > DateTime.UtcNow,
                cancellationToken);
        }

        /// <summary>
        /// Gets the creation time of the most recent token for a user.
        /// </summary>
        /// <param name="userId">The user ID to check.</param>
        /// <param name="cancellationToken">Cancellation token for async operations.</param>
        /// <returns>Creation time of latest token or null if none exist.</returns>
        public async Task<DateTime?> GetLastTokenCreationTimeAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Where(rt => rt.UserId == userId)
                .OrderByDescending(rt => rt.CreatedAtUtc)
                .Select(rt => rt.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
