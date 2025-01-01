using System;
using System.Linq.Expressions;
using System.Reflection;

using Quine.HRCatalog;

namespace Quine.Schemas.Core.Repository;

/// <summary>
/// Provides weakly-typed access to an arbitrary (possibly nested) member of <typeparamref name="TEntity"/>.
/// Equality is based on <see cref="QdbValueAttributes.DbName"/> field since names must be unique in any
/// parameter set or table.  The two instances are "equal" if they refer to the same property OR have the
/// same <c>DbName</c> attribute. (A collection of value accessors can contain unique instance of either.)
/// </summary>
/// <typeparam name="TEntity">The type containing the member for which the accessor is being created.</typeparam>
public sealed class QdbValueAccessor<TEntity> : IEquatable<QdbValueAccessor<TEntity>>, ICloneable
{
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="accessor">Expression defining the "path" to the member.</param>
    /// <param name="valueConverter">Optional value converted.</param>
    public QdbValueAccessor
        (
        Expression<Func<TEntity, object>> accessor,
        QdbValueAttributes attributes = null
        )
    {
        MemberExpression = accessor;
        Attributes = attributes ?? new();
        GetProperties(out var rawMemberExpression, out MemberInfo, out MemberType, out MemberName);
        if (Attributes.DbName is null)
            Attributes.DbName = MemberName;
        if (!Attributes.DbType.HasValue)
            Attributes.DbType = QdbValueAttributes.InferDbType(MemberType);
        Attributes.Validate();
        CompileAccessors((MemberExpression)rawMemberExpression, out MemberGet, out MemberSet);
    }

    public QdbValueAccessor<TEntity> Clone() {
        var ret = (QdbValueAccessor<TEntity>)MemberwiseClone();
        ret.Attributes = Attributes.Clone();
        return ret;
    }
    object ICloneable.Clone() => Clone();

    private void GetProperties
        (
        out Expression rawMemberExpression,
        out MemberInfo memberInfo,
        out Type memberType,
        out string memberName
        )
    {
        rawMemberExpression = MemberExpression.Body;
        if (rawMemberExpression.NodeType == ExpressionType.Convert)
            rawMemberExpression = ((UnaryExpression)rawMemberExpression).Operand;

        var me = (MemberExpression)rawMemberExpression;
        memberInfo = me.Member;
        memberType = me.Type;
        if (me.Expression.NodeType == ExpressionType.Parameter) memberName = me.Member.Name;
        else memberName = null;
    }

    private void CompileAccessors
        (
        MemberExpression rawMemberExpression,
        out Func<TEntity, object> get,
        out Action<TEntity, object> set
        )
    {
        var pThis = MemberExpression.Parameters[0];
        var pValue = Expression.Parameter(typeof(object), "v");
        
        get = Expression.Lambda<Func<TEntity, object>>(MemberExpression.Body, pThis).Compile();

        if (rawMemberExpression.Member is PropertyInfo pi && !pi.CanWrite) {
            set = null;
            return;
        }

        set = Expression.Lambda<Action<TEntity, object>>
            (
                Expression.Assign
                (
                    rawMemberExpression,
                    Expression.Convert(pValue, MemberType)
                ),
                pThis, pValue
            ).Compile();
    }

    /// <summary>
    /// The expression passed to ctor.
    /// </summary>
    public readonly LambdaExpression MemberExpression;

    /// <summary>
    /// Member being accessed.  The member's declaring type will be different from <c>T</c> if the member is inheritde
    /// OR is nested (accessed through multi-step expression).
    /// </summary>
    public readonly MemberInfo MemberInfo;

    /// <summary>
    /// The actual type of the member referred to by <see cref="MemberExpression"/>.
    /// </summary>
    public readonly Type MemberType;

    /// <summary>
    /// The name of the property referred to by <see cref="MemberExpression"/> if it refers to a direct member of <c>T</c>;
    /// null otherwise.  (Example: accessor <c>x => x.Name</c> would set this to <c>Name</c>, whereas <c>x => x.Address.City</c>
    /// would set this to null.)
    /// </summary>
    public readonly string MemberName;

    /// <summary>
    /// Delegate for getting the raw value (without conversion) of the property.
    /// </summary>
    public readonly Func<TEntity, object> MemberGet;

    /// <summary>
    /// Delegate for setting the raw value (without conversion) of the property.
    /// </summary>
    public readonly Action<TEntity, object> MemberSet;

    /// <summary>
    /// Value attributes on the db-side.  If converter is provided, this overrides the converter's attributes.
    /// </summary>
    public QdbValueAttributes Attributes { get; private set; }

    /// <summary>
    /// Converts member value from <paramref name="source"/> object to database value.
    /// </summary>
    /// <returns>
    /// Member value converted to database value according to <see cref="Attributes"/>.
    /// </returns>
    public object Get(TEntity source) => Attributes.GetDbValue(MemberGet(source));

    /// <summary>
    /// Converts database value <paramref name="value"/> to member value according to <see cref="Attributes"/>
    /// and sets it on <paramref name="target"/>.
    /// </summary>
    public void Set(TEntity target, object value) => MemberSet(target, Attributes.GetMemberValue(value));

    public bool Equals(QdbValueAccessor<TEntity> other) => other != null &&
        (
            Attributes.Equals(other.Attributes)
            ||
            (
                MemberInfo.MetadataToken == other.MemberInfo.MetadataToken
                &&
                MemberInfo.Module.ModuleHandle.Equals(other.MemberInfo.Module.ModuleHandle)
            )
        );
    public override bool Equals(object obj) => obj is QdbValueAccessor<TEntity> other && Equals(other);
    public override int GetHashCode() => MemberInfo.GetHashCode();
}
