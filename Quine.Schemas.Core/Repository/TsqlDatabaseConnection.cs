using System;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

using Quine.HRCatalog;

namespace Quine.Schemas.Core.Repository;

/// <summary>
/// Base class for implementing <see cref="IQdbConnection"/> on SQL syntax.
/// Uses TSQL syntax for parameters, etc.
/// </summary>
public abstract class TsqlDatabaseConnection : IQdbConnection
{
    public IQdbSource Database => CheckDisposed()._database;
    private IQdbSource _database;
    
    public DbConnection DbConnection => CheckDisposed()._connection;
    private DbConnection _connection;
    
    public IQdbTransaction Transaction => CheckDisposed()._transaction;
    private IQdbTransaction _transaction;
    
    protected TsqlDatabaseConnection(IQdbSource database, DbConnection connection) {
        _database = database;
        _connection = connection;
    }

    protected TsqlDatabaseConnection CheckDisposed() => _connection is not null ? this : 
        throw new ObjectDisposedException(GetType().FullName);
    
    public virtual void Dispose() {
        if (_connection is null)
            return;
        _connection.Dispose();
        _connection = null;
        GC.SuppressFinalize(this);
    }

    public abstract QdbCommand CreateCommand(CommandType commandType, string commandText);
    public abstract Task<int> GetSequenceNumber(string name, int length = 1);

    // Properties are used below so we get disposed check "for free".
    
    public async Task<IQdbTransaction> BeginTransactionAsync(IsolationLevel level = IsolationLevel.Unspecified) {
        QHEnsure.State(CheckDisposed()._transaction is null);
        return _transaction = await CreateTransactionAsync(level);
    }

    /// <summary>
    /// Override this method in conjunction with deriving from <see cref="TsqlTransaction"/> to create a global transaction lock.
    /// </summary>
    protected virtual async Task<IQdbTransaction> CreateTransactionAsync(IsolationLevel level) {
        var tx = await DbConnection.BeginTransactionAsync(level);
        return new TsqlTransaction(this, tx);
    }

    protected class TsqlTransaction : IQdbTransaction
    {
        public IQdbConnection Connection => CheckDisposed()._connection;
        private IQdbConnection _connection;

        public DbTransaction DbTransaction => CheckDisposed()._transaction;
        private DbTransaction _transaction;

        public event Action<Exception> RollbackErrorHandler;

        public TsqlTransaction(IQdbConnection connection, DbTransaction transaction) {
            _connection = connection;
            _transaction = transaction;
        }

        protected TsqlTransaction CheckDisposed() => _transaction is not null ? this :
            throw new ObjectDisposedException(GetType().FullName);

        public virtual void Dispose() {
            if (_transaction is null)
                return;

            var eh = RollbackErrorHandler;
            try {
                _transaction.Rollback();
            }
            catch (Exception e) when (eh is not null) {
                eh(e);
            }
            finally {
                try { _transaction.Dispose(); }
                catch { }
                _transaction = null;
                ((TsqlDatabaseConnection)_connection)._transaction = null;
            }
        }

        public virtual async Task CommitAsync() {
            QHEnsure.State(Connection.Transaction == this); // Also disposed check
            try {
                await _transaction.CommitAsync();
            }
            finally {
                try { await _transaction.DisposeAsync(); }
                catch { }
                _transaction = null;
                ((TsqlDatabaseConnection)_connection)._transaction = null;
            }
        }

    }
}
