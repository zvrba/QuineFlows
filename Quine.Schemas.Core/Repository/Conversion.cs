using System;
using System.Collections.Generic;
using System.Data;
using Quine.HRCatalog;

namespace Quine.Schemas.Core.Repository;

/// <summary>
/// Describes value attributes on the database side for the member and provides conversions between .NET and DB types.
/// Deriving from this class allows implementation of custom conversions.  Equality is based only on <see cref="DbName"/>
/// because parameters and columns must be uniquely named in any DB invocation.
/// </summary>
public class QdbValueAttributes : IEquatable<QdbValueAttributes>, ICloneable
{
    public QdbValueAttributes
        (
        string dbName = null,
        DbType? dbType = null,
        int? dbSize = null,
        ParameterDirection direction = ParameterDirection.Input,
        int? keyOrder = null
        )
    {
        DbName = dbName;
        DbType = dbType;
        DbSize = dbSize;
        Direction = direction;
        KeyOrder = keyOrder;
    }

    public QdbValueAttributes Clone() => (QdbValueAttributes)MemberwiseClone();
    object ICloneable.Clone() => Clone();

    public QdbValueAttributes With
        (
        string dbName = null,
        ParameterDirection? direction = null
        )
    {
        var ret = Clone();
        if (dbName != null)
            ret.DbName = dbName;
        if (direction.HasValue)
            ret.Direction = direction.Value;
        return ret;
    }

    public string DbName { get; internal set; }
    public DbType? DbType { get; internal set; }
    public int? DbSize { get; internal set; }
    public ParameterDirection Direction { get; internal set; }
    public int? KeyOrder { get; internal set; }

    public virtual object GetDbValue(object memberValue) => memberValue is null ? DBNull.Value : memberValue;
    public virtual object GetMemberValue(object dbValue) => (dbValue is null or DBNull) ? null : dbValue;

    public bool Equals(QdbValueAttributes other) => DbName.Equals(other.DbName);
    public sealed override bool Equals(object obj) => obj is QdbValueAttributes dbva && Equals(dbva);
    public sealed override int GetHashCode() => DbName.GetHashCode();

    internal void Validate() {
        QHEnsure.NotEmpty(DbName);
        QHEnsure.State(DbType.HasValue);
        QHEnsure.State(
                (
                    DbType != System.Data.DbType.AnsiString &&
                    DbType != System.Data.DbType.StringFixedLength &&
                    DbType != System.Data.DbType.String &&
                    DbType != System.Data.DbType.Binary
                )
                ||
                (DbSize.HasValue && DbSize.Value != 0));
        QHEnsure.State(!KeyOrder.HasValue || KeyOrder >= 0);
    }

    internal static DbType InferDbType(Type t) {
        {
            var ut = Nullable.GetUnderlyingType(t);
            if (ut is not null)
                t = ut;
        }
        if (t.IsEnum) {
            t = Enum.GetUnderlyingType(t);
            QHEnsure.State(t == typeof(int));
        }
        return TypeMap[t];
    }

    private static readonly IReadOnlyDictionary<Type, DbType> TypeMap = new Dictionary<Type, DbType>() {
        { typeof(byte[]), System.Data.DbType.Binary },

        { typeof(bool), System.Data.DbType.Boolean },
        { typeof(byte), System.Data.DbType.Byte },
        { typeof(short), System.Data.DbType.Int16 },
        { typeof(int), System.Data.DbType.Int32 },
        { typeof(long), System.Data.DbType.Int64 },

        { typeof(float), System.Data.DbType.Single },
        { typeof(double), System.Data.DbType.Double },

        // Date cannot be used as parameter! 
        { typeof(DateTime), System.Data.DbType.DateTime2 },
        { typeof(DateTimeOffset), System.Data.DbType.DateTimeOffset },
        { typeof(TimeSpan), System.Data.DbType.Time },
        { typeof(Guid), System.Data.DbType.Guid },

        // TODO: DataTable for structured/object types!
    };
}

public sealed class QdbEnumAttributes<TEnum> : QdbValueAttributes where TEnum : Enum
{
    public QdbEnumAttributes
        (
        string dbName = null,
        ParameterDirection direction = ParameterDirection.Input,
        int? keyOrder = null
        ) :
        base
        (
            dbName,
            System.Data.DbType.Int32,
            direction: direction,
            keyOrder: keyOrder
        )
    { }

    public override object GetDbValue(object memberValue) =>
        memberValue is null ? DBNull.Value : (int)memberValue;

    public override object GetMemberValue(object dbValue) =>
        (dbValue is null or DBNull) ? null : (TEnum)dbValue;
}

public sealed class QdbPathComponentsAttributes : QdbValueAttributes
{
    // Cannot be a key column.
    public QdbPathComponentsAttributes
        (
        int dbSize,
        string dbName = null,
        int? keyOrder = null,
        ParameterDirection direction = ParameterDirection.Input
        )
        :
        base
        (
            dbName,
            System.Data.DbType.String,
            dbSize,
            direction,
            keyOrder
        ) 
    { }

    public override object GetDbValue(object memberValue) {
        if (memberValue is null)
            return DBNull.Value;
        
        var s = ((PathComponents)memberValue).NormalizedString;
        if (s is null)
            return DBNull.Value;

        if (s.Length > DbSize)
            throw new System.IO.InvalidDataException($"PathComponents length exceed the maximul allowed size of {DbSize} for column {DbName}.");

        return s;
    }

    public override object GetMemberValue(object dbValue) =>
        (dbValue is null or DBNull) ? PathComponents.Empty : PathComponents.Make((string)dbValue);
}

public sealed class QdbNormalStringAttributes<TStringTraits> : QdbValueAttributes
    where TStringTraits : struct, INormalStringTraits
{
    public QdbNormalStringAttributes
        (
        string dbName = null,
        ParameterDirection direction = ParameterDirection.Input,
        int? keyOrder = null
        ) 
        : base
        (
            dbName,
            TStringTraits.IsUTF8 ? System.Data.DbType.AnsiString : System.Data.DbType.String,
            TStringTraits.MaxLength,
            direction,
            keyOrder
        )
    { }

    public override object GetDbValue(object memberValue) {
        if (memberValue is null)
            return DBNull.Value;
        var ns = (NormalString<TStringTraits>)memberValue;
        INormalStringTraits.EnsureLength<TStringTraits>(ns);
        return ns.IsNull ? DBNull.Value : ns.Value;
    }

    public override object GetMemberValue(object dbValue) {
        return dbValue is null or DBNull ? null : (NormalString<TStringTraits>)(string)dbValue;
    }
}

public sealed class QdbXmlAttributes<TValue> : QdbValueAttributes
{
    public QdbXmlAttributes
        (
        string dbName = null,
        ParameterDirection direction = ParameterDirection.Input
        ) :
        base
        (
            dbName,
            System.Data.DbType.Xml,
            direction: direction
        )
    { }

    public override object GetDbValue(object memberValue) =>
        memberValue is null ? DBNull.Value : Schemas.DCSerializer.Serialize((TValue)memberValue);

    public override object GetMemberValue(object dbValue) =>
        (dbValue is null or DBNull) ? null : Schemas.DCSerializer.Deserialize<TValue>((string)dbValue);
}
