using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Quine.HRCatalog;

namespace Quine.Schemas.Core.Repository;

public partial class EntityCollection<TEntity>
{
    private const string ModificationDisallowed = "Modification through cloning view is disallowed; changes must be performed through the original collection.";

    private class DirectStrategy : ICollection<TEntity>
    {
        internal DirectStrategy(EntityCollection<TEntity> owner) => this._Owner = owner;
        
        protected readonly EntityCollection<TEntity> _Owner;
        
        protected virtual dynamic Collection {
            get => _Owner.collection;
            set => _Owner.collection = value;
        }
        
        protected virtual TEntity Clone(TEntity entity) => entity;
        
        private static dynamic GetId(TEntity entity) => ((IDynamicIdentity)entity)._IdAsDynamic;

        public virtual bool IsReadOnly => false;

        public int Count => Collection.Count;

        public TEntity this[int index] => Clone(Collection[index]);
        public TEntity this[dynamic key] => Clone(Collection[key]);

        public bool Contains(TEntity entity) {
            QHEnsure.State(KeyType != null);
            return Collection.ContainsKey(GetId(entity));
        }

        public virtual void CopyTo(TEntity[] array, int index) {
            // Cloning is performed in the derived class.
            if (Collection is IList<TEntity> l) {
                l.CopyTo(array, index);
            } else {
                foreach (TEntity v in Collection.Values)
                    array[index++] = v;
            }
        }

        public virtual void Clear() => Collection = Empty;
        
        public virtual bool Add(TEntity entity) {
            if (KeyType == null) {
                Collection = ((ImmutableList<TEntity>)Collection).Add(Clone(entity));
                return true;
            }
            if (!Collection.ContainsKey(GetId(entity))) {
                Collection = Collection.Add(GetId(entity), Clone(entity));
                return true;
            }
            return false;
        }

        void ICollection<TEntity>.Add(TEntity item) => Add(item);

        public virtual bool Remove(TEntity entity) {
            QHEnsure.State(KeyType != null);
            if (Collection.ContainsKey(GetId(entity))) {
                Collection = Collection.Remove(GetId(entity));
                return true;
            }
            return false;
        }

        public IEnumerator<TEntity> GetEnumerator() {
            var enumerable = (IEnumerable<TEntity>)(Collection is IList<TEntity> ? Collection : Collection.Values);
            return enumerable.Select(Clone).GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        // Selected dictionary methods

        public virtual void Replace(TEntity entity) {
            QHEnsure.State(KeyType != null);
            Collection = Collection.SetItem(GetId(entity), Clone(entity));
        }

        public bool ContainsKey(dynamic key) {
            QHEnsure.State(KeyType != null);
            return Collection.ContainsKey(key);
        }

        public bool TryGetValue(dynamic key, out TEntity value) {
            QHEnsure.State(KeyType != null);
            if (Collection.TryGetValue(key, out value)) {
                value = Clone(value);
                return true;
            }
            return false;
        }
    }

    private sealed class CloningStrategy : DirectStrategy
    {
        internal CloningStrategy(EntityCollection<TEntity> owner) : base(owner) { }

        protected override dynamic Collection {
            get => _Owner.source.collection;
            set => throw new NotSupportedException(ModificationDisallowed);
        }

        protected override TEntity Clone(TEntity entity) => DCSerializer.CloneGeneric(entity);

        public override bool IsReadOnly => true;

        public override void CopyTo(TEntity[] array, int index) {
            base.CopyTo(array, index);
            for (int i = 0; i < Count; ++i)
                array[index + i] = DCSerializer.CloneGeneric(array[index + i]);
        }

        public override void Clear() => throw new NotSupportedException(ModificationDisallowed);
        public override bool Add(TEntity entity) => throw new NotSupportedException(ModificationDisallowed);
        public override bool Remove(TEntity entity) => throw new NotSupportedException(ModificationDisallowed);
        public override void Replace(TEntity entity) => throw new NotSupportedException(ModificationDisallowed);
    }
}
