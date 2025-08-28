using Domain.Common;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public abstract class BaseRepository<T> : IBaseRepository<T> where T : class
    {
        protected readonly DbContext Context;
        protected readonly DbSet<T> DbSet;

        protected BaseRepository(DbContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            DbSet = context.Set<T>();
        }

        public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await DbSet.FindAsync(new object[] { id }, cancellationToken);
            return entity;
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery().ToListAsync(cancellationToken);
        }

        public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            SetTimestamps(entity, isUpdate: false);
            await DbSet.AddAsync(entity, cancellationToken);
            return entity;
        }

        public virtual Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            cancellationToken.ThrowIfCancellationRequested();

            SetTimestamps(entity, isUpdate: true);
            var trackedEntity = DbSet.Update(entity);
            return Task.FromResult(trackedEntity.Entity);
        }

        public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            cancellationToken.ThrowIfCancellationRequested();

            // Check if entity supports soft delete
            if (entity is ISoftDeletable softDeletable)
            {
                softDeletable.IsDeleted = true;
                SetTimestamps(entity, isUpdate: true);
                DbSet.Update(entity);
            }
            else
            {
                DbSet.Remove(entity);
            }

            return Task.CompletedTask;
        }

        public virtual async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity != null)
            {
                await DeleteAsync(entity, cancellationToken);
            }
        }

        public virtual async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .AnyAsync(e => EF.Property<Guid>(e, "Id") == id, cancellationToken);
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .Where(predicate)
                .ToListAsync(cancellationToken);
        }

        public virtual async Task<T?> FindFirstAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .FirstOrDefaultAsync(predicate, cancellationToken);
        }

        public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery().CountAsync(cancellationToken);
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
        {
            return await GetFilteredQuery()
                .CountAsync(predicate, cancellationToken);
        }

        /// <summary>
        /// Override this method to apply global filters (like soft delete)
        /// </summary>
        protected virtual IQueryable<T> GetFilteredQuery()
        {
            var query = DbSet.AsQueryable();

            // Apply soft delete filter if entity supports it
            if (typeof(ISoftDeletable).IsAssignableFrom(typeof(T)))
            {
                query = query.Where(e => !EF.Property<bool>(e, nameof(ISoftDeletable.IsDeleted)));
            }

            return query;
        }

        /// <summary>
        /// Set timestamps for audit fields
        /// </summary>
        protected virtual void SetTimestamps(T entity, bool isUpdate)
        {
            var now = DateTime.UtcNow;

            if (!isUpdate && entity is IHasCreationTime creatable)
            {
                creatable.CreatedAtUtc = now;
            }

            if (entity is IHasModificationTime modifiable)
            {
                modifiable.UpdatedAtUtc = now;
            }
        }
    }
}
