using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

using Quine.HRCatalog;

namespace Quine.Schemas.Core.Repository;

/// <summary>
/// Provides access to transactions and table operations.  Default method implementations are targeted towards @-style parameters
/// which are supported by most DB providers.
/// </summary>
public interface IQdbConnection : IDisposable
{
    /// <summary>
    /// Owning connection factory.
    /// </summary>
    IQdbSource Database { get; }

    /// <summary>
    /// The underlying <c>DbConnection</c>.
    /// </summary>
    DbConnection DbConnection { get; }

    /// <summary>
    /// Non-null if a transaction is active.  Only one transaction can be active at a time and nested transactions
    /// are not supported.
    /// </summary>
    IQdbTransaction Transaction { get; }

    /// <summary>
    /// Creates a command tied to this connection and transaction if any is active at the time of invocation.
    /// </summary>
    QdbCommand CreateCommand(CommandType commandType, string commandText);

    /// <summary>
    /// Begins a transaction if possible.
    /// </summary>
    /// <returns>
    /// An instance of <see cref="IQdbTransaction"/> or <c>null</c> if <see cref="Transaction"/> was active when this method was called.
    /// </returns>
    Task<IQdbTransaction> BeginTransactionAsync(IsolationLevel level = IsolationLevel.Unspecified);

    /// <summary>
    /// Atomically retrieves the next available number from a named sequence.
    /// This method must be invoked within an active transaction.
    /// </summary>
    /// <param name="name">Name of the sequence from which to get the numbers.</param>
    /// <param name="length">Length of the returned gap; if 0 only the current value is returned.</param>
    /// <returns>
    /// If <paramref name="length"/> is 0, the current value is returned.  Otherwise it returns the lowest number N
    /// such that N, N+1, ..., N+length-1 are unique and won't be repeated by the sequence.
    /// </returns>
    Task<int> GetSequenceNumber(string name, int length = 1);

    /// <summary>
    /// Creates a reusable command for finding an entity by key.
    /// </summary>
    /// <param name="accessor">
    /// Defines the subset of columns of <typeparamref name="TEntity"/> to fetch.
    /// Passing <c>default</c> will use the entity's default accessor.
    /// </param>
    IQdbEntityReadCommand<TEntity> CreateFind<TEntity>(EntityAccessor<TEntity> accessor)
        where TEntity : class, IQdbEntity<TEntity>, new() => new QdbEntityOperation<TEntity>.FindOperation(this, accessor);

    /// <summary>
    /// Finds an entity by its primary key.
    /// </summary>
    /// <param name="accessor">
    /// Defines the subset of properties of <typeparamref name="TEntity"/> to fetch.  Passing <c>default</c> will use the entity's default accessor.
    /// </param>
    /// <param name="key">
    /// Key components.  The order of the values must match the order of key columns specified in the accessor for <typeparamref name="TEntity"/>.
    /// </param>
    async Task<TEntity> FindAsync<TEntity>(EntityAccessor<TEntity> accessor, params object[] key)
        where TEntity : class, IQdbEntity<TEntity>, new()
    {
        QHEnsure.State(key.Length == TEntity.EntityAccessor.KeyMembers.Length);
        using var command = CreateFind(accessor);
        return await command.ExecuteAsync(key);
    }

    /// <summary>
    /// Creates a reusable command for inserting an entity.
    /// </summary>
    /// <param name="accessor">
    /// Non-default accessor should be specified only when computed columns on database-side exist; these must be omitted during insert.
    /// </param>
    IQdbEntityWriteCommand<TEntity> CreateInsert<TEntity>(EntityAccessor<TEntity> accessor = default)
        where TEntity : class, IQdbEntity<TEntity>, new() => new QdbEntityOperation<TEntity>.InsertOperation(this, accessor);

    /// <summary>
    /// Creates a new entity.  An exception is thrown if the key already exists.
    /// Specifying a non-default value for <paramref name="accessor"/> allows use of projections for <typeparamref name="TEntity"/>.
    /// </summary>
    async Task InsertAsync<TEntity>(TEntity entity, EntityAccessor<TEntity> accessor = default)
        where TEntity : class, IQdbEntity<TEntity>, new()
    {
        using var command = CreateInsert(accessor);
        var c = await command.ExecuteAsync(entity);
        QHEnsure.State(c == 1);
    }

    /// <summary>
    /// Creates a reusable command for updating an entity.
    /// This method cannot be used to modify key fields.
    /// </summary>
    /// <param name="accessor">
    /// Defines the subset of properties of <typeparamref name="TEntity"/> to update.  Must include all key fields.
    /// Passing <c>default</c> will use the entity's default accessor.
    /// </param>
    IQdbEntityWriteCommand<TEntity> CreateUpdate<TEntity>(EntityAccessor<TEntity> accessor = default)
        where TEntity : class, IQdbEntity<TEntity>, new() => new QdbEntityOperation<TEntity>.UpdateOperation(this, accessor);

    /// <summary>
    /// Updates an existing entity.
    /// This method cannot be used to modify key fields.
    /// Specifying a non-default value for <paramref name="accessor"/> allows use of projections for <typeparamref name="TEntity"/>.
    /// </summary>
    /// <returns>True if the entity was found by its primary key and updated.</returns>
    async Task<bool> UpdateAsync<TEntity>(TEntity entity, EntityAccessor<TEntity> accessor = default)
        where TEntity : class, IQdbEntity<TEntity>, new()
    {
        using var command = CreateUpdate(accessor);
        var c = await command.ExecuteAsync(entity);
        QHEnsure.State(c <= 1); // Keyed entity, at most one row affected.
        return c == 1;
    }

    /// <summary>
    /// Creates a reusable command for deleting an entity.
    /// </summary>
    /// <param name="accessor">
    /// Defines the subset of properties of <typeparamref name="TEntity"/> to fetch.  Passing <c>default</c> will use the entity's default accessor.
    /// </param>
    IQdbEntityWriteCommand<TEntity> CreateDelete<TEntity>()
        where TEntity : class, IQdbEntity<TEntity>, new() => new QdbEntityOperation<TEntity>.DeleteOperation(this);

    /// <summary>
    /// Attempts to delete <paramref name="entity"/> by its primary key.
    /// </summary>
    async Task<bool> DeleteAsync<TEntity>(TEntity entity)
        where TEntity : class, IQdbEntity<TEntity>, new()
    {
        using var command = CreateDelete<TEntity>();
        var c = await command.ExecuteAsync(entity);
        QHEnsure.State(c <= 1);
        return c == 1;
    }
}
