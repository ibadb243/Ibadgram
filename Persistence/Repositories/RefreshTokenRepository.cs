using Application.Interfaces.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Persistence.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Persistence.Repositories
{
    public class RefreshTokenRepository : IRefreshTokenRepository
    {
        private readonly ApplicationDbContext _context;

        public RefreshTokenRepository(
            ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<RefreshToken?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Id == id, cancellationToken);
        }

        public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);
        }

        public async Task<RefreshToken> AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
        {
            refreshToken.CreatedAtUtc = DateTime.UtcNow;

            await _context.RefreshTokens.AddAsync(refreshToken);

            return refreshToken;
        }

        public async Task<RefreshToken> UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default)
        {
            _context.RefreshTokens.Update(refreshToken);

            return refreshToken;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var token = await GetByIdAsync(id, cancellationToken);
            if (token == null)
            {
                throw new Exception($"RefreshToken with ID {id} not found");
            }

            _context.RefreshTokens.Remove(token);
        }

        public async Task<IEnumerable<RefreshToken>> GetUserTokensAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .OrderByDescending(rt => rt.CreatedAtUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<RefreshToken>> GetActiveUserTokensAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            return await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.ExpiresAtUtc > now)
                .OrderByDescending(rt => rt.CreatedAtUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task<RefreshToken?> GetActiveTokenByValueAsync(string token, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            return await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == token && !rt.IsRevoked && rt.ExpiresAtUtc > now, cancellationToken);
        }

        public async Task RevokeTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            var refreshToken = await GetByTokenAsync(token, cancellationToken);
            if (refreshToken == null)
            {
                throw new Exception($"RefreshToke with token {token} not found");
            }

            if (refreshToken.IsRevoked)
            {
                return;
            }

            refreshToken.IsRevoked = true;
            _context.RefreshTokens.Update(refreshToken);
        }

        public async Task RevokeTokenAsync(Guid tokenId, CancellationToken cancellationToken = default)
        {
            var refreshToken = await GetByIdAsync(tokenId, cancellationToken);
            if (refreshToken == null)
            {
                throw new Exception($"RefreshToke with ID {tokenId} not found");
            }

            if (refreshToken.IsRevoked)
            {
                return;
            }

            refreshToken.IsRevoked = true;
            _context.RefreshTokens.Update(refreshToken);
        }

        public async Task RevokeAllUserTokensAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var tokens = await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked)
                .ToListAsync(cancellationToken);

            foreach (var token in tokens)
            {
                token.IsRevoked = true;
                _context.RefreshTokens.Update(token);
            }
        }

        public async Task RevokeExpiredTokensAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var expiredTokens = await _context.RefreshTokens
                .Where(rt => !rt.IsRevoked && rt.ExpiresAtUtc <= now)
                .ToListAsync(cancellationToken);

            foreach (var token in expiredTokens)
            {
                token.IsRevoked = true;
                _context.RefreshTokens.Update(token);
            }
        }

        public async Task<bool> IsTokenValidAsync(string token, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            return await _context.RefreshTokens
                .AnyAsync(rt => rt.Token == token && !rt.IsRevoked && rt.ExpiresAtUtc > now, cancellationToken);
        }

        public async Task<bool> TokenExistsAsync(string token, CancellationToken cancellationToken = default)
        {
            return await _context.RefreshTokens
                .AnyAsync(rt => rt.Token == token, cancellationToken);
        }

        public async Task CleanupExpiredTokensAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var expiredTokens = await _context.RefreshTokens
                .Where(rt => rt.ExpiresAtUtc <= now)
                .ToListAsync(cancellationToken);

            if (expiredTokens.Any())
            {
                _context.RefreshTokens.RemoveRange(expiredTokens);
            }
        }

        public async Task CleanupRevokedTokensAsync(DateTime olderThan, CancellationToken cancellationToken = default)
        {
            var revokedTokens = await _context.RefreshTokens
                .Where(rt => rt.IsRevoked && rt.CreatedAtUtc < olderThan)
                .ToListAsync(cancellationToken);

            if (revokedTokens.Any())
            {
                _context.RefreshTokens.RemoveRange(revokedTokens);
            }
        }

        public async Task<int> GetActiveTokensCountAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            return await _context.RefreshTokens
                .Where(rt => rt.UserId == userId && !rt.IsRevoked && rt.ExpiresAtUtc > now)
                .CountAsync(cancellationToken);
        }

        public async Task<int> GetTotalActiveTokensCountAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            return await _context.RefreshTokens
                .Where(rt => !rt.IsRevoked && rt.ExpiresAtUtc > now)
                .CountAsync(cancellationToken);
        }

        public async Task<DateTime?> GetLastTokenCreationTimeAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.RefreshTokens
                .Where(rt => rt.UserId == userId)
                .OrderByDescending(rt => rt.CreatedAtUtc)
                .Select(rt => rt.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
