using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;

namespace Quine.Schemas.Core.Repository;

/// <summary>
/// Represents the result of database query.  If <typeparamref name="TEntity"/> implements <see cref="IIdentity{T}"/>,
/// the collection must be used as a dictionary.  Otherwise, the collection must be used as an indexed list.  The
/// implementation uses immutable list or dictionary, so it is safe for concurrent use.
/// </summary>
/// <typeparam name="TEntity">
/// Value type returned by the query.
/// </typeparam>
public partial class EntityCollection<TEntity> : ICollection<TEntity>
    where TEntity : class 
{
    #region Static ctor and dynamic helpers

    private static readonly object Empty;

    static EntityCollection() {
        foreach (var intf in typeof(TEntity).GetInterfaces()) {
            Type ii;
            for
            (
            ii = intf;
            ii != null && (!ii.IsConstructedGenericType || ii.GetGenericTypeDefinition() != typeof(IIdentity<>));
            ii = ii.BaseType
            ) ;
            if (ii != null) {
                KeyType = ii.GetGenericArguments()[0];
                break;
            }
        }
        if (KeyType != null) {
            Empty = typeof(ImmutableDictionary<,>)
                .MakeGenericType(KeyType, typeof(TEntity))
                .GetField("Empty", BindingFlags.Static | BindingFlags.Public)
                .GetValue(null);
        } else {
            Empty = ImmutableList<TEntity>.Empty;
        }
    }

    #endregion

    /// <summary>
    /// If not null, this is the key type as implemented by <see cref="IIdentity{T}"/>.
    /// </summary>
    public static readonly Type KeyType;

    private readonly EntityCollection<TEntity> source;
    private readonly DirectStrategy strategy;
    
    private dynamic collection;

    /// <summary>
    /// Constructor.
    /// </summary>
    public EntityCollection() {
        this.collection = Empty;
        this.strategy = new DirectStrategy(this);
    }

    // For AsCloning.
    private EntityCollection(EntityCollection<TEntity> source) {
        this.source = source;
        this.strategy = new CloningStrategy(this);
    }

    /// <summary>
    /// Converts this collection to a "cloning" collection where returned entities are cloned and no modifications to
    /// the collection are allowed. The returned collection tracks all changes made to the original (non-cloning) collection.
    /// If <c>this</c> is already cloning, <c>this</c> will be returned (i.e., a new "fork" is NOT created).
    /// </summary>
    public EntityCollection<TEntity> AsCloning => IsCloning ? this : new(this);

    /// <summary>
    /// When true, the all entity instances are deeply cloned through serialization.
    /// (Both when adding to and fetching from the collection.)
    /// </summary>
    public bool IsCloning => strategy is CloningStrategy;

    #region ICollection / quasi-list-dictionary

    public bool IsReadOnly => false;
    public int Count => strategy.Count;

    /// <summary>
    /// Usable only when <see cref="KeyType"/> is null.
    /// </summary>
    public TEntity this[int index] => strategy[index];

    /// <summary>
    /// Usable only when <see cref="KeyType"/> is not null.
    /// </summary>
    public TEntity this[dynamic key] => strategy[key];

    public IEnumerator<TEntity> GetEnumerator() => strategy.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public void Clear() => strategy.Clear();

    /// <summary>
    /// Supported only when <see cref="KeyType"/> is not null.
    /// </summary>
    public bool Contains(TEntity entity) => strategy.Contains(entity);

    public void CopyTo(TEntity[] array, int index) => strategy.CopyTo(array, index);

    /// <summary>
    /// An entity can always be added.  No duplicate check is performed when <see cref="KeyType"/> is null.
    /// </summary>
    /// <returns>True if the entity was added.</returns>
    public bool Add(TEntity entity) => strategy.Add(entity);
    void ICollection<TEntity>.Add(TEntity item) => Add(item);

    /// <summary>
    /// Supported only when <see cref="KeyType"/> is not null.
    /// </summary>
    public bool Remove(TEntity entity) => strategy.Remove(entity);

    /// <summary>
    /// Supported only when <see cref="KeyType"/> is not null.
    /// </summary>
    public void Replace(TEntity entity) => strategy.Replace(entity);

    /// <summary>
    /// Supported only when <see cref="KeyType"/> is not null.
    /// </summary>
    public bool ContainsKey(dynamic key) => strategy.ContainsKey(key);

    /// <summary>
    /// Supported only when <see cref="KeyType"/> is not null.
    /// </summary>
    public bool TryGetValue(dynamic key, out TEntity value) => strategy.TryGetValue(key, out value);

#endregion
}
