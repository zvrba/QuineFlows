using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Quine.HRCatalog;

namespace Quine.Schemas.Core.Repository;

public interface IQdbEntityReadCommand<TEntity> : IDisposable
    where TEntity : class, IQdbEntity<TEntity>
{
    Task<bool> ExecuteAsync(TEntity entity);
    Task<TEntity> ExecuteAsync(params object[] key);
}

public interface IQdbEntityWriteCommand<TEntity> : IDisposable
    where TEntity : class, IQdbEntity<TEntity>
{
    Task<int> ExecuteAsync(TEntity entity);
    Task<int> ExecuteAsync(params object[] key);
}

/// <summary>
/// Encapsulates operations over a single, keyed entity.
/// </summary>
/// <typeparam name="TEntity"></typeparam>
internal abstract class QdbEntityOperation<TEntity> where TEntity : class, IQdbEntity<TEntity>, new()
{
    static QdbEntityOperation() {
        QHEnsure.State(TEntity.EntityAccessor.IsKeyed);
    }

    private static readonly string KeyCondition =
        string.Join(" AND ", TEntity.EntityAccessor.KeyMembers
            .Select(m => $"{m.Attributes.DbName} = @{m.Attributes.DbName}"));

    private EntityAccessor<TEntity> accessor;
    private QdbCommand command;

    private QdbEntityOperation(IQdbConnection connection, EntityAccessor<TEntity> accessor) {
        IQdbSource.GetDefaultAccessor(ref accessor);
        this.accessor = accessor;
        this.command = GetCommand(connection);
    }

    protected abstract QdbCommand GetCommand(IQdbConnection connection);

    public void Dispose() {
        command?.Dispose();
        command = null;
    }

    private void SetCommandKey(TEntity entity) {
        foreach (var m in TEntity.EntityAccessor.KeyMembers)
            command.DbCommand.Parameters["@" + m.Attributes.DbName].Value = m.Get(entity);
    }

    private void SetCommandKey(object[] key) {
        for (int i = 0; i < key.Length; ++i)
            command.DbCommand.Parameters["@" + TEntity.EntityAccessor.KeyMembers[i].Attributes.DbName].Value = key[i];
    }

    internal sealed class FindOperation : QdbEntityOperation<TEntity>, IQdbEntityReadCommand<TEntity>
    {
        internal FindOperation
            (
            IQdbConnection connection,
            EntityAccessor<TEntity> accessor
            ) : base(connection, accessor) { }

        protected override QdbCommand GetCommand(IQdbConnection connection) {
            var sb = new StringBuilder(256);
            sb.Append("SELECT ");
            sb.AppendJoin(',', accessor.Members.Select(m => m.Attributes.DbName));
            sb.AppendLine();
            sb.AppendLine($"FROM {connection.Database.GetEntityDbName<TEntity>()}");
            sb.Append("WHERE ");
            sb.Append(KeyCondition);
            sb.AppendLine(";");

            var command = connection.CreateCommand(CommandType.Text, sb.ToString());
            try {
                foreach (var m in TEntity.EntityAccessor.KeyMembers)
                    command.CreateParameterIfNotExists(m);
                return command;
            }
            catch {
                command.Dispose();
                throw;
            }
        }

        public async Task<bool> ExecuteAsync(TEntity entity) {
            SetCommandKey(entity);
            return await ReadEntity(entity);
        }

        public async Task<TEntity> ExecuteAsync(params object[] key) {
            SetCommandKey(key);
            var e = new TEntity();
            return await ReadEntity(e) ? e : null;
        }

        private async Task<bool> ReadEntity(TEntity entity) {
            await using var reader = await command.DbCommand.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return false;
            foreach (var ma in accessor.Members)
                ma.Set(entity, reader[ma.Attributes.DbName]);
            QHEnsure.State(!await reader.ReadAsync());
            return true;
        }
    }

    internal sealed class InsertOperation : QdbEntityOperation<TEntity>, IQdbEntityWriteCommand<TEntity>
    {
        internal InsertOperation
            (
            IQdbConnection connection,
            EntityAccessor<TEntity> accessor
            ) : base(connection, accessor) { }

        protected override QdbCommand GetCommand(IQdbConnection connection) {
            var sb = new StringBuilder(256);
            sb.Append($"INSERT INTO {connection.Database.GetEntityDbName<TEntity>()} (");
            sb.Append(string.Join(',', accessor.Members.Select(x => x.Attributes.DbName)));
            sb.AppendLine(")");
            sb.Append("VALUES (");
            sb.Append(string.Join(',', accessor.Members.Select(x => "@" + x.Attributes.DbName)));
            sb.AppendLine(");");
            sb.AppendLine("SET @__ROWCOUNT = @@ROWCOUNT;");

            var command = connection.CreateCommand(CommandType.Text, sb.ToString());
            try {
                command.CreateParameters(accessor);
                command.Parameters.__ROWCOUNT += new QdbValueAttributes(dbType: DbType.Int32, direction: ParameterDirection.Output);
                return command;
            }
            catch {
                command.Dispose();
                throw;
            }
        }

        public Task<int> ExecuteAsync(params object[] key) => throw new NotSupportedException("InsertOperation: by key only.");

        public async Task<int> ExecuteAsync(TEntity entity) {
            await command.ExecuteNonQueryAsync(entity);
            return command.Parameters.__ROWCOUNT;
        }
    }

    internal sealed class UpdateOperation : QdbEntityOperation<TEntity>, IQdbEntityWriteCommand<TEntity>
    {
        internal UpdateOperation
            (
            IQdbConnection connection,
            EntityAccessor<TEntity> accessor
            ) : base(connection, QHEnsure.Value(accessor, !accessor.Members.Any(x => x.Attributes.KeyOrder.HasValue))) { }

        protected override QdbCommand GetCommand(IQdbConnection connection) {
            var sb = new StringBuilder(256);
            sb.AppendLine($"UPDATE {connection.Database.GetEntityDbName<TEntity>()}");
            sb.Append("SET ");
            sb.AppendLine(
                string.Join(',',
                    accessor.Members
                        .Where(m => !m.Attributes.KeyOrder.HasValue)
                        .Select(m => $"{m.Attributes.DbName} = @{m.Attributes.DbName}")));
            sb.Append("WHERE ");
            sb.Append(KeyCondition);
            sb.AppendLine(";");
            sb.AppendLine("SET @__ROWCOUNT = @@ROWCOUNT;");

            var command = connection.CreateCommand(CommandType.Text, sb.ToString());
            try {
                command.CreateParameters(accessor);
                command.Parameters.__ROWCOUNT += new QdbValueAttributes(dbType: DbType.Int32, direction: ParameterDirection.Output);
                foreach (var m in TEntity.EntityAccessor.KeyMembers)
                    command.CreateParameterIfNotExists(m);
                return command;
            }
            catch {
                command.Dispose();
                throw;
            }
        }

        public Task<int> ExecuteAsync(params object[] key) => throw new NotSupportedException("UpdateOperation: by key only.");

        public async Task<int> ExecuteAsync(TEntity entity) {
            SetCommandKey(entity);
            await command.ExecuteNonQueryAsync(entity);
            return command.Parameters.__ROWCOUNT;
        }
    }

    internal sealed class DeleteOperation : QdbEntityOperation<TEntity>, IQdbEntityWriteCommand<TEntity>
    {
        internal DeleteOperation(IQdbConnection connection) : base(connection, default) { }

        protected override QdbCommand GetCommand(IQdbConnection connection) {
            var sb = new StringBuilder(256);
            sb.AppendLine($"DELETE FROM {connection.Database.GetEntityDbName<TEntity>()}");
            sb.Append("WHERE ");
            sb.Append(KeyCondition);
            sb.AppendLine(";");
            sb.AppendLine("SET @__ROWCOUNT = @@ROWCOUNT;");

            var command = connection.CreateCommand(CommandType.Text, sb.ToString());
            try {
                foreach (var m in TEntity.EntityAccessor.KeyMembers)
                    command.CreateParameterIfNotExists(m);
                command.Parameters.__ROWCOUNT += new QdbValueAttributes(dbType: DbType.Int32, direction: ParameterDirection.Output);
                return command;
            }
            catch {
                command.Dispose();
                throw;
            }
        }

        public async Task<int> ExecuteAsync(TEntity entity) {
            SetCommandKey(entity);
            await command.DbCommand.ExecuteNonQueryAsync();
            return command.Parameters.__ROWCOUNT;
        }

        public async Task<int> ExecuteAsync(params object[] key) {
            SetCommandKey(key);
            await command.DbCommand.ExecuteNonQueryAsync();
            return command.Parameters.__ROWCOUNT;
        }
    }
}
