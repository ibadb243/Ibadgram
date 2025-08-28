using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Infrastructure.Data;
using System.Data;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<UnitOfWork> _logger;
        private IDbContextTransaction? _currentTransaction;
        private bool _disposed = false;

        // Lazy-loaded repositories
        private IUserRepository? _userRepository;
        private IRefreshTokenRepository? _refreshTokenRepository;
        private IMentionRepository? _mentionRepository;
        private IUserMentionRepository? _userMentionRepository;
        private IChatMentionRepository? _chatMentionRepository;
        private IChatRepository? _chatRepository;
        private IChatMemberRepository? _chatMemberRepository;
        private IMessageRepository? _messageRepository;

        public UnitOfWork(
            ApplicationDbContext context,
            ILogger<UnitOfWork> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Repository properties with lazy initialization
        public IUserRepository UserRepository =>
            _userRepository ??= new UserRepository(_context);

        public IRefreshTokenRepository RefreshTokenRepository =>
            _refreshTokenRepository ??= new RefreshTokenRepository(_context);

        public IMentionRepository MentionRepository =>
            _mentionRepository ??= new MentionRepository(_context);

        public IUserMentionRepository UserMentionRepository =>
            _userMentionRepository ??= new UserMentionRepository(_context);

        public IChatMentionRepository ChatMentionRepository =>
            _chatMentionRepository ??= new ChatMentionRepository(_context);

        public IChatRepository ChatRepository =>
            _chatRepository ??= new ChatRepository(_context);

        public IChatMemberRepository ChatMemberRepository =>
            _chatMemberRepository ??= new ChatMemberRepository(_context);

        public IMessageRepository MessageRepository =>
            _messageRepository ??= new MessageRepository(_context);

        public bool HasActiveTransaction => _currentTransaction != null;

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                throw new InvalidOperationException("A concurrency conflict occurred while saving changes.", ex);
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("An error occurred while saving changes to the database.", ex);
            }
        }

        public async Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
        {
            if (_currentTransaction != null)
            {
                throw new InvalidOperationException("A transaction is already active. Complete the current transaction before starting a new one.");
            }

            _currentTransaction = await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        }

        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction == null)
            {
                throw new InvalidOperationException("No active transaction to commit.");
            }

            try
            {
                await SaveChangesAsync(cancellationToken);
                await _currentTransaction.CommitAsync(cancellationToken);
            }
            catch (Exception)
            {
                await RollbackTransactionAsync(cancellationToken);
                throw;
            }
            finally
            {
                await DisposeTransactionAsync();
            }
        }

        public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (_currentTransaction == null)
                return;

            try
            {
                await _currentTransaction.RollbackAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Log the rollback exception but don't throw it
                // This prevents masking the original exception that caused the rollback
                _logger.LogError(ex, "Error during transaction rollback");
            }
            finally
            {
                await DisposeTransactionAsync();
            }
        }

        private async Task DisposeTransactionAsync()
        {
            if (_currentTransaction != null)
            {
                await _currentTransaction.DisposeAsync();
                _currentTransaction = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    _currentTransaction?.Dispose();
                    _context?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing UnitOfWork: {ex.Message}");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }

        ~UnitOfWork()
        {
            Dispose(false);
        }
    }
}
