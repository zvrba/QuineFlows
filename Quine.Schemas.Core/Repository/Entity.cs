using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;

using Quine.HRCatalog;

namespace Quine.Schemas.Core.Repository;

public interface IQdbEntity<TEntity> where TEntity : IQdbEntity<TEntity>
{
    private static readonly Lazy<ConstructorInfo> ParameterlessCtorInfo = new(GetParameterlessCtor);

    /// <summary>
    /// Provides the etnity's DB name (view, table, etc.). May return null for "entities" that are parameter blocks.
    /// </summary>
    abstract static string EntityDbName { get; }

    /// <summary>
    /// Provides accessor for mapping the entity to db operations.
    /// </summary>
    abstract static EntityAccessor<TEntity> EntityAccessor { get; }

    /// <summary>
    /// Creates an entity from a <c>DbDataReader</c>.  Default implementation invokes the parameterless constructor.
    /// </summary>
    /// <returns>A new entity instance.</returns>
    virtual static TEntity CreateFromDataRow(System.Data.Common.DbDataReader reader, EntityAccessor<TEntity> accessor) {
        var e = (TEntity)ParameterlessCtorInfo.Value.Invoke(null);
        foreach (var ma in accessor.Members)
            ma.Set(e, reader[ma.Attributes.DbName]);
        return e;
    }

    private static ConstructorInfo GetParameterlessCtor() =>
        typeof(TEntity).GetConstructor(Type.EmptyTypes) ??
            throw new InvalidOperationException($"Type {typeof(TEntity).FullName} has no parameterless ctor.  Custom IEntityReader must be provided.");
}

/// <summary>
/// Every table, view or command parameter block must declare a readonly static instance of this class.
/// </summary>
/// <typeparam name="TEntity">The type containing the member for which the accessor is being created.</typeparam>
public readonly struct EntityAccessor<TEntity> : ICloneable where TEntity : IQdbEntity<TEntity>
{
    /// <summary>
    /// Constructor.  Key members must be specified in order and start at index 0.
    /// </summary>
    /// <param name="members">Members to map.  All <c>DbName</c> attributes must be unique; an exception is thrown otherwise.</param>
    public EntityAccessor(params QdbValueAccessor<TEntity>[] members) {
        var b1 = ImmutableArray.CreateBuilder<QdbValueAccessor<TEntity>>(members.Length);
        foreach (var m in members) {
            QHEnsure.State(!b1.Contains(m));
            b1.Add(m);
        }
        Members = b1.ToImmutable();

        if (Members.Any(x => x.Attributes.KeyOrder.HasValue)) {
            KeyMembers = Members.Where(x => x.Attributes.KeyOrder.HasValue).ToImmutableArray();
            for (int i = 0; i < KeyMembers.Length; ++i)
                QHEnsure.State(KeyMembers[i].Attributes.KeyOrder.Value == i);
        }
    }

    private EntityAccessor(ImmutableArray<QdbValueAccessor<TEntity>> members) => Members = members;

    public EntityAccessor<TEntity> Clone() => new(Members.Select(x => x.Clone()).ToImmutableArray());
    object ICloneable.Clone() => Clone();

    /// <summary>
    /// This is true only for default instance.
    /// </summary>
    public bool IsDefault => Members.IsDefault;

    /// <summary>
    /// True if this instance contains the whole key defined by <typeparamref name="TEntity"/>.
    /// If the entity is keyless, this property is false.
    /// </summary>
    public bool IsKeyed => !KeyMembers.IsDefault;

    /// <summary>
    /// This list contains all members.
    /// </summary>
    public readonly ImmutableArray<QdbValueAccessor<TEntity>> Members;

    /// <summary>
    /// This list contains only key members.  The instance is <c>default</c> (<c>IsDefault</c> is true) when there are no keys.
    /// </summary>
    /// <seealso cref="IsKeyed"/>
    public readonly ImmutableArray<QdbValueAccessor<TEntity>> KeyMembers;

    /// <summary>
    /// Retrieves an accessor by its <c>DbName</c> attribute.
    /// </summary>
    /// <param name="dbName"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    public QdbValueAccessor<TEntity> this[string dbName] => Members.FirstOrDefault(x => x.Attributes.DbName == dbName) ??
        throw CreateInvalidNameException(dbName);

    /// <summary>
    /// Retains only members named in <paramref name="members"/>.
    /// Throws exception if the name does not exist.
    /// </summary>
    /// <returns>A new instance containing only the named members.</returns>
    public EntityAccessor<TEntity> Retain(params string[] members) {
        var @this = this;
        return new(members.Select(x => @this[x]).ToImmutableArray());
    }

    /// <summary>
    /// Removes members named in <paramref name="members"/>.
    /// Throws exception if the name does not exist.
    /// </summary>
    /// <returns>A new instance with the named members removed.</returns>
    public EntityAccessor<TEntity> Remove(params string[] members) {
        var b = Members.ToBuilder();
        foreach (var m in members) {
            QHEnsure.State(b.Remove(this[m]));
        }
        return new(b.ToImmutable());
    }

    static KeyNotFoundException CreateInvalidNameException(string dbName) =>
        new KeyNotFoundException($"Accessor for {dbName} was not found in the entity map for {typeof(TEntity).FullName}.");
}
 