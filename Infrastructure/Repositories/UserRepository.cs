using Domain.Entities;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;


namespace Infrastructure.Repositories
{
    public class UserRepository : BaseRepository<User>, IUserRepository
    {
        public UserRepository(ApplicationDbContext context) : base(context) { }

        public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email))
                return null;

            return await GetFilteredQuery()
                .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
        }

        public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            return await GetFilteredQuery()
                .AnyAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
        }

        public async Task<User?> GetWithMembershipsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Include(u => u.Memberships)
                    .ThenInclude(m => m.Chat)
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        }

        public async Task<User?> GetWithRefreshTokensAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Include(u => u.RefreshTokens.Where(rt => !rt.IsRevoked && rt.ExpiresAtUtc > DateTime.UtcNow))
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        }

        public async Task<User?> GetByEmailWithPasswordAsync(string email, string passwordHash, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(passwordHash))
                return null;

            return await GetFilteredQuery()
                .FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant() && u.PasswordHash == passwordHash, cancellationToken);
        }

        public async Task ConfirmEmailAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdAsync(userId, cancellationToken);
            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} not found");

            user.EmailConfirmed = true;
            await UpdateAsync(user, cancellationToken);
        }

        public async Task UpdatePasswordAsync(Guid userId, string passwordHash, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdAsync(userId, cancellationToken);
            if (user == null)
                throw new KeyNotFoundException($"User with ID {userId} not found");

            user.PasswordHash = passwordHash;
            await UpdateAsync(user, cancellationToken);
        }

        public async Task<IEnumerable<User>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return Enumerable.Empty<User>();

            var term = searchTerm.Trim().ToLowerInvariant();
            return await GetFilteredQuery()
                .Where(u => u.Firstname.ToLower().Contains(term) ||
                           (u.Lastname != null && u.Lastname.ToLower().Contains(term)))
                .Take(20) // Limit results
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<User>> GetVerifiedUsersAsync(CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Where(u => u.EmailConfirmed)
                .ToListAsync(cancellationToken);
        }

        public async Task<(IEnumerable<User> Users, int TotalCount)> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100; // Limit max page size

            var query = GetFilteredQuery();
            var totalCount = await query.CountAsync(cancellationToken);
            var users = await query
                .OrderBy(u => u.Firstname)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (users, totalCount);
        }

        public async Task<int> GetVerifiedUsersCountAsync(CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .CountAsync(u => u.EmailConfirmed, cancellationToken);
        }
    }
}
