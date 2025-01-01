using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

using Quine.HRCatalog;

namespace Quine.Schemas.Core.Repository;

/// <summary>
/// Reads a collection of entities from a result set.
/// </summary>
/// <remarks>
/// Usage notes: the class implementing <see cref="IQdbConnection"/> can subscribe to <see cref="OnDisposed"/> and
/// ensure that output parameters are written back to the parameter block after disposal.  Dispose methods automatically
/// clear <see cref="OnDisposed"/> event, so unsubscription is not necessary.
/// </remarks>
public sealed class QdbReader : IDisposable
{
    public readonly IQdbConnection Connection;
    public readonly DbDataReader Reader;
    public event Action<QdbReader> OnDisposed;

    public QdbReader(IQdbConnection connection, DbDataReader reader) {
        Connection = QHEnsure.NotNull(connection);
        Reader = QHEnsure.NotNull(reader);
    }

    public void Dispose() {
        try {
            Reader.Dispose();
            OnDisposed?.Invoke(this);
        }
        finally {
            OnDisposed = null;
        }
    }

    /// <summary>
    /// Reads all entities from the current result set and adds them to <paramref name="entities"/>.
    /// On completion, the reader  is positioned to the next result set and <see cref="HasNextResultSet"/>
    /// is set to true if the next result set exists.
    /// </summary>
    /// <param name="entities">Collection to which the read entities are added.</param>
    /// <param name="accessor">Accessor if a projection is used.</param>
    /// <returns>
    /// True if the next result set is available.
    /// </returns>
    public async Task<bool> ReadResultSetAsync<TEntity>(ICollection<TEntity> entities, EntityAccessor<TEntity> accessor = default)
        where TEntity : class, IQdbEntity<TEntity>, new()
    {
        IQdbSource.GetDefaultAccessor(ref accessor);
        while (await Reader.ReadAsync())
            entities.Add(TEntity.CreateFromDataRow(Reader, accessor));
        return HasNextResultSet = await Reader.NextResultAsync();
    }

    /// <summary>
    /// Set after all entities from the current result set have been consumed.
    /// </summary>
    public bool HasNextResultSet { get; private set; }

    /// <summary>
    /// Asynchronously enumerates the current result set, one item at a time.
    /// On completion, the reader is positioned to the next result set.  <see cref="HasNextResultSet"/> is set to true
    /// if the next result set exists, otherwise false.
    /// </summary>
    /// <param name="accessor">Accessor if a projection is used.</param>
    public async IAsyncEnumerable<TEntity> EnumerateResultSetAsync<TEntity>(EntityAccessor<TEntity> accessor = default)
        where TEntity : class, IQdbEntity<TEntity>, new()
    {
        IQdbSource.GetDefaultAccessor(ref accessor);
        while (await Reader.ReadAsync())
            yield return TEntity.CreateFromDataRow(Reader, accessor);
        HasNextResultSet = await Reader.NextResultAsync();
    }
}
