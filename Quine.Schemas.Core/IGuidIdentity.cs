using System;
using System.Collections.Generic;

namespace Quine.Schemas.Core
{
    /// <summary>
    /// For keyed generic collections where the type of the id is not known at compile-time.
    /// </summary>
    public interface IDynamicIdentity
    {
        dynamic _IdAsDynamic { get; }
    }

    public interface IIdentity<T> : IDynamicIdentity where T : IEquatable<T>
    {
        T Id { get; }
        dynamic IDynamicIdentity._IdAsDynamic => Id;
    }

    public class IdentityEquals<T, I> : IEqualityComparer<T>
        where T : IIdentity<I>
        where I : IEquatable<I>
    {
        public bool Equals(T i1, T i2) => i1.Id.Equals(i2.Id);
        public int GetHashCode(T obj) => obj.Id.GetHashCode();
    }
}
