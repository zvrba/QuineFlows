using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace Quine.Schemas.Core.Repository;

/// <summary>
/// Transaction handle with customizable handling of rollback errors.  Disposing a transaction without committing it
/// will roll it back.
/// </summary>
public interface IQdbTransaction : IDisposable
{
    /// <summary>
    /// The underlying <c>DbTransaction</c>.
    /// </summary>
    DbTransaction DbTransaction { get; }

    /// <summary>
    /// Owning connection.  <c>null</c> for disposed transactions.
    /// </summary>
    IQdbConnection Connection { get; }

    /// <summary>
    /// This property exists to assist with logging of errors in combination with <c>await using</c>.  
    /// If non-null, <see cref="RollbackAsync"/> invokes this delegate when exception occurs instead of throwing it.
    /// If null, a message will be logged to <see cref="IQdbSource.ShellTrace"/>.
    /// </summary>
    event Action<Exception> RollbackErrorHandler;

    /// <summary>
    /// Commits the transaction.
    /// </summary>
    Task CommitAsync();
}

