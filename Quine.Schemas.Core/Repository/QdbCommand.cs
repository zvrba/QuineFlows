using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Dynamic;
using System.Threading.Tasks;

using Quine.HRCatalog;

namespace Quine.Schemas.Core.Repository;

/// <summary>
/// Wrapper for a <c>DbCommand</c> with simplified parameter creation.  See remarks.
/// </summary>
/// <remarks>
/// <para>
/// Parameters are exposed throughs <see cref="Parameters"/> object.  An SQL parameter named <c>@PValue</c> is created by either
/// <list type="bullet">
/// <item>
/// Simply assigning the value to <see cref="Parameters"/> collection, as in <c>Parameters.PValue = 12;</c>.  This creates an
/// input-only parameter and works only for simple values (i.e. not strings or byte arrays).
/// </item>
/// <item>
/// By preconfiguring the parameter through <see cref="QdbValueAttributes"/>, as in <c>Parameters.PValue += new DbPathComponentsAttributes();</c>.
/// Only size, type and direction are read from the attributes, and the parameter name is taken from the member name (<c>PValue</c>).
/// </item>
/// </list>
/// Once a value is assigned to the parameter, it can be reassigned, but its type must be the same as on the first assignment.
/// Parameter values are read by simple acces, e.g., <c>var v = Parameters.PValue;</c>.
/// </para>
/// <para>
/// Parameters for the command can also be created explicitly with <see cref="CreateParameters{TParameters}(Quine.Schemas.Core.Repository.EntityAccessor{TParameters})"/> method.
/// </para>
/// </remarks>
public sealed class QdbCommand : IDisposable
{
    private readonly Dictionary<string, QdbValueAttributes> pattrs = new(8);
    private object accessor;

    public QdbCommand(IQdbConnection connection, DbCommand command) {
        this.Connection = connection;
        this.command = QHEnsure.NotNull(command);
    }

    private QdbCommand CheckDisposed() => command is not null ? this : throw new ObjectDisposedException(nameof(QdbCommand));

    public void Dispose() {
        if (command is not null) {
            command.Dispose();
            command = null;
        }
    }

    public IQdbConnection Connection { get; }
    public DbCommand DbCommand => CheckDisposed().command;
    private DbCommand command;
    
    public dynamic Parameters {
        get {
            CheckDisposed();
            _Parameters ??= new ParameterSetProxy(this);
            return _Parameters;
        }
    }
    private dynamic _Parameters;

    /// <summary>
    /// Creates command parameters from the given parameter accessor.  The accessor is remembered for subsequent calls to
    /// <see cref="WriteParameters{TParameters}(TParameters)"/> and <see cref="ReadParameters{TParameters}(TParameters)"/>.
    /// </summary>
    /// <param name="accessor">
    /// Specifying a non-default value for <paramref name="accessor"/> allows use of projections for <typeparamref name="TParameters"/>.
    /// </param>
    public void CreateParameters<TParameters>(EntityAccessor<TParameters> accessor = default)
        where TParameters : IQdbEntity<TParameters>
    {
        QHEnsure.State(CheckDisposed().accessor is null);
        IQdbSource.GetDefaultAccessor(ref accessor);
        this.accessor = accessor;
        foreach (var m in accessor.Members)
            QHEnsure.State(CreateParameterIfNotExists(m));
    }

    public bool CreateParameterIfNotExists<TParameters>(QdbValueAccessor<TParameters> va) {
        var pn = "@" + va.Attributes.DbName;
        if (DbCommand.Parameters.Contains(pn))
            return false;
        var p = DbCommand.CreateParameter();
        p.ParameterName = pn;
        ConfigureParameter(p, va.Attributes);
        DbCommand.Parameters.Add(p);
        return true;
    }

    /// <summary>
    /// Transfers values from <paramref name="parameters"/> into input parameters of this command.
    /// </summary>
    public void WriteParameters<TParameters>(TParameters parameters)
            where TParameters : IQdbEntity<TParameters> 
    {
        var accessor = (EntityAccessor<TParameters>)QHEnsure.NotNull(CheckDisposed().accessor);
        foreach (var ma in accessor.Members)
            if (IsInputParameter(ma.Attributes.Direction))
                DbCommand.Parameters["@" + ma.Attributes.DbName].Value = ma.Get(parameters);
    }

    /// <summary>
    /// Transfers values from this command into output parameters of <paramref name="parameters"/>.
    /// </summary>
    public void ReadParameters<TParameters>(TParameters parameters)
        where TParameters : class, IQdbEntity<TParameters>
    {
        var accessor = (EntityAccessor<TParameters>)QHEnsure.NotNull(CheckDisposed().accessor);
        foreach (var ma in accessor.Members)
            if (IsOutputParameter(ma.Attributes.Direction))
                ma.Set(parameters, DbCommand.Parameters["@" + ma.Attributes.DbName].Value);
    }

    /// <summary>
    /// Executes a non-query.
    /// <paramref name="parameters"/> may be null for commands with parameters configured through <see cref="Parameters"/>.
    /// </summary>
    public async Task<int> ExecuteNonQueryAsync<TParameters>(TParameters parameters)
        where TParameters : class, IQdbEntity<TParameters>
    {
        QHEnsure.State(CheckDisposed().accessor is EntityAccessor<TParameters>);
        if (parameters is not null)
            WriteParameters(parameters);
        var ret = await DbCommand.ExecuteNonQueryAsync();
        if (parameters is not null)
            ReadParameters(parameters);
        return ret;
    }

    /// <summary>
    /// Executes a scalar query.
    /// <paramref name="parameters"/> may be null for commands with parameters configured through <see cref="Parameters"/>.
    /// </summary>
    public async Task<object> ExecuteScalarAsync<TParameters>(TParameters parameters)
        where TParameters : class, IQdbEntity<TParameters>
    {
        QHEnsure.State(CheckDisposed().accessor is EntityAccessor<TParameters>);
        if (parameters is not null)
            WriteParameters(parameters);
        var ret = await DbCommand.ExecuteScalarAsync();
        if (parameters is not null)
            ReadParameters(parameters);
        return ret;
    }

    /// <summary>
    /// Executes a result-returning query.
    /// <paramref name="parameters"/> may be null for commands with parameters configured through <see cref="Parameters"/>.
    /// </summary>
    public async Task<QdbReader> ExecuteReaderAsync<TParameters>(TParameters parameters)
        where TParameters : class, IQdbEntity<TParameters>
    {
        if (parameters is not null)
            WriteParameters(parameters);
        var reader = await DbCommand.ExecuteReaderAsync();
        var ret = new QdbReader(Connection, reader);
        ret.OnDisposed += OnDisposed;
        return ret;

        void OnDisposed(QdbReader er) {
            QHEnsure.State(er == ret);
            ret.OnDisposed -= OnDisposed;
            if (parameters is not null)
                ReadParameters(parameters);
        }
    }

    public async Task<QdbReader> ExecuteReaderAsync() {
        var reader = await DbCommand.ExecuteReaderAsync();
        return new QdbReader(Connection, reader);
    }

    private static bool IsInputParameter(ParameterDirection d) => d switch {
        ParameterDirection.Input => true,
        ParameterDirection.InputOutput => true,
        _ => false
    };

    private static bool IsOutputParameter(ParameterDirection d) => d switch {
        ParameterDirection.Output => true,
        ParameterDirection.InputOutput => true,
        ParameterDirection.ReturnValue => true,
        _ => false
    };

    static void ConfigureParameter(DbParameter parameter, QdbValueAttributes dbva) {
        if (dbva.DbType.HasValue)
            parameter.DbType = dbva.DbType.Value;
        if (dbva.DbSize.HasValue)
            parameter.Size = dbva.DbSize > 0 ? dbva.DbSize.Value + 4 : dbva.DbSize.Value; // NB! Allow larger transfer to also trigger error on db-side in case of truncation
        parameter.Direction = dbva.Direction;
    }

    // Implements += that accepts DbValueAttributes.  The attribute's DbName is ignored as it's taken from the member name.
    private sealed class ParameterProxy : DynamicObject {
        private readonly QdbCommand owner;
        private readonly string parameterName;

        internal ParameterProxy(QdbCommand owner, string parameterName) {
            this.owner = owner;
            this.parameterName = QHEnsure.Value(parameterName, parameterName[0] == '@');
        }

        public override bool TryBinaryOperation(BinaryOperationBinder binder, object arg, out object result) {
            if (binder.Operation != System.Linq.Expressions.ExpressionType.AddAssign || arg is not QdbValueAttributes dbva) {
                result = null;
                return false;
            }

            var parameter = owner.DbCommand.CreateParameter();
            parameter.ParameterName = parameterName;
            ConfigureParameter(parameter, dbva);
            owner.DbCommand.Parameters.Add(parameter);
            owner.pattrs.Add(parameterName, dbva);

            result = this;
            return true;
        }
    }

    private sealed class ParameterSetProxy : DynamicObject {
        private readonly QdbCommand owner;

        internal ParameterSetProxy(QdbCommand owner) => this.owner = owner;

        public override bool TrySetMember(SetMemberBinder binder, object value) {
            // Tricky: we can end up here in case of P.V += a, where LHS of += is ParameterProxy the return value from which
            // is the proxy itself.  In that case, the parameter is only configured and there's no value to write.
            if (value is null)
                return false;
            if (value is ParameterProxy)
                return true;

            var qname = "@" + binder.Name;

            DbParameter dbp;
            if (!owner.DbCommand.Parameters.Contains(qname)) {
                QHEnsure.State(value is not (byte[] or string));
                dbp = owner.DbCommand.CreateParameter();
                dbp.ParameterName = qname;
                dbp.DbType = QdbValueAttributes.InferDbType(value.GetType());
                owner.DbCommand.Parameters.Add(dbp);
            }
            else {
                dbp = owner.DbCommand.Parameters[qname];
                QHEnsure.State(dbp.Value is (null or DBNull) || dbp.Value.GetType() == value.GetType());
            }

            if (owner.pattrs.TryGetValue(qname, out var dbva))
                value = dbva.GetDbValue(value);

            dbp.Value = value;
            return true;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            var qname = "@" + binder.Name;
            if (owner.DbCommand.Parameters.Contains(qname)) {
                result = owner.DbCommand.Parameters[qname].Value;
                if (owner.pattrs.TryGetValue(qname, out var dbva))
                    result = dbva.GetMemberValue(result);
            } else {
                result = new ParameterProxy(owner, qname);
            }
            return true;
        }
    }
}
