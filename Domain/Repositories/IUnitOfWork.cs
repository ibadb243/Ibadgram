using System.Data;

namespace Domain.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IUserRepository UserRepository { get; }
        IRefreshTokenRepository RefreshTokenRepository { get; }
        IMentionRepository MentionRepository { get; }
        IUserMentionRepository UserMentionRepository { get; }
        IChatMentionRepository ChatMentionRepository { get; }
        IChatRepository ChatRepository { get; }
        IChatMemberRepository ChatMemberRepository { get; }
        IMessageRepository MessageRepository { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
        Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default);
        Task CommitTransactionAsync(CancellationToken cancellationToken = default);
        Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
        bool HasActiveTransaction { get; }
    }
}
