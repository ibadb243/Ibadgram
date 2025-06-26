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
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(
            ApplicationDbContext context,
            ILogger<UserRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => !u.IsDeleted)
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        }

        public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => !u.IsDeleted)
                .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        }

        public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => !u.IsDeleted)
                .ToListAsync(cancellationToken);
        }

        public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
        {
            user.CreatedAtUtc = DateTime.UtcNow;

            await _context.Users.AddAsync(user, cancellationToken);

            _logger.LogInformation("User created with ID: {UserId}", user.Id);
            return user;
        }

        public async Task<User> UpdateAsync(User user, CancellationToken cancellationToken = default)
        {
            _context.Users.Update(user);

            _logger.LogInformation("User updated with ID: {UserId}", user.Id);
            return user;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdAsync(id, cancellationToken);
            if (user == null)
            {
                throw new Exception($"User with ID {id} not found");
            }

            user.IsDeleted = true;
            _context.Users.Update(user);

            _logger.LogInformation("User soft deleted with ID: {UserId}", id);
        }

        public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => !u.IsDeleted)
                .AnyAsync(u => u.Id == id, cancellationToken);
        }

        public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => !u.IsDeleted)
                .AnyAsync(u => u.Email == email, cancellationToken);
        }

        public async Task<User?> GetWithMembershipsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => !u.IsDeleted)
                .Include(u => u.Memberships)
                    .ThenInclude(m => m.Chat)
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        }

        public async Task<User?> GetWithRefreshTokensAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => !u.IsDeleted)
                .Include(u => u.RefreshTokens.Where(rt => !rt.IsRevoked && rt.ExpiresAtUtc > DateTime.UtcNow))
                .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        }

        public async Task<User?> GetByEmailWithPasswordAsync(
            string email, 
            string passwordHash, 
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => !u.IsDeleted)
                .FirstOrDefaultAsync(u => u.Email == email && u.PasswordHash == passwordHash, cancellationToken);
        }

        public async Task ConfirmEmailAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var user = await GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                throw new Exception($"User with ID {userId} not found");
            }

            user.EmailConfirmed = true;
            _context.Users.Update(user);

            _logger.LogInformation("User email was confirmed with ID: {UserId}", userId);
        }

        public async Task UpdatePasswordSaltAndPasswordHashAsync(
            Guid userId, 
            string passwordSalt, 
            string passwordHash, 
            CancellationToken cancellationToken = default)
        {
            var user = await GetByIdAsync(userId, cancellationToken);
            if (user == null)
            {
                throw new Exception($"User with ID {userId} not found");
            }

            user.PasswordHash = passwordHash;
            await UpdateAsync(user, cancellationToken);
        }

        public async Task<IEnumerable<User>> SearchByNameAsync(string searchTerm, CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => !u.IsDeleted)
                .Where(u => u.Firstname.Contains(searchTerm) ||
                           (u.Lastname != null && u.Lastname.Contains(searchTerm)))
                .ToListAsync(cancellationToken);
        }

        public async Task<IEnumerable<User>> GetVerifiedUsersAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => !u.IsDeleted && u.EmailConfirmed)
                .ToListAsync(cancellationToken);
        }

        public async Task<(IEnumerable<User> Users, int TotalCount)> GetPagedAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _context.Users.Where(u => !u.IsDeleted);

            var totalCount = await query.CountAsync(cancellationToken);
            var users = await query
                .OrderBy(u => u.Firstname)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (users, totalCount);
        }

        public async Task<int> GetTotalUsersCountAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => !u.IsDeleted)
                .CountAsync(cancellationToken);
        }

        public async Task<int> GetVerifiedUsersCountAsync(CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .Where(u => !u.IsDeleted && u.EmailConfirmed)
                .CountAsync(cancellationToken);
        }
    }
}
