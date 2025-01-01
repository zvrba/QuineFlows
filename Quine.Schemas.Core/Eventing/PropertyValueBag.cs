using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Quine.Schemas.Core.Eventing;

/// <summary>
/// Serializable bag of typed properties.  "Simple" values, except enums, are strongly-typed; others are converted
/// to string by invoking <c>ToString</c> through reflection.  NOT thread-safe.  This is the main extensibility
/// point: derive from this class to store more structured data rather than putting bits into the dictionary.
/// Use <see cref="RegisterKnownType(Type)"/> to support additional serializable types in the data dictionary.
/// </summary>
[DataContract(Namespace = XmlNamespaces.Core_1_0)]
[KnownType(nameof(GetKnownTypes))]
public abstract class PropertyValueBag
{
    /// <summary>
    /// Flag to <see cref="AddMembers(object, int)"/> to include public properties.
    /// </summary>
    public const int IncludeProperties = 1;

    /// <summary>
    /// Flag to <see cref="AddMembers(object, int)"/> to include public fields.
    /// </summary>
    public const int IncludeFields = 2;

    /// <summary>
    /// Schema extension point: all additional derived classes and object types used as values in <see cref="Data"/>
    /// dictionary must be registered.
    /// </summary>
    /// <param name="t"></param>
    public static void RegisterKnownType(Type t) => KnownTypes.Add(t);

    /// <summary>
    /// Represents a typed value converted to untyped string.
    /// </summary>
    [DataContract(Namespace = XmlNamespaces.Core_1_0)]
    public struct UntypedValue
    {
        /// <summary>
        /// Fully-qaulified name of the type.
        /// </summary>
        [DataMember]
        public readonly string Type;

        /// <summary>
        /// String representation as returned by <c>ToString</c>.
        /// </summary>
        [DataMember]
        public readonly string Value;

        internal UntypedValue(string type, string value) {
            Type = type;
            Value = value;
        }
    }

    /// <summary>
    /// Collection of key-value pairs.  The property never returns <c>null</c>, but data is allocated only on first access.
    /// </summary>
    /// <seealso cref="HasData"/>
    public IReadOnlyDictionary<string, object> Data {
        get {
            if (_Data == null)
                _Data = new(8);
            return _Data;
        }
    }
    [DataMember(Name = "Data")]
    private Dictionary<string, object> _Data;

    /// <summary>
    /// Used to check whether data dictionary is present without allocating it.
    /// </summary>
    public bool HasData => _Data != null;

#if false   // REMOVED MEMBERS, DO NOT REINTRODUCE WITH THE SAME NAME!!!
    // Type is removed because the concrete class' name is in xsi:type attribute.
    /// <summary>
    /// The "type" of the property bag.  May be null.  Mostly useful when multiple property bags are present
    /// in the same object.
    /// </summary>
    public string Type => type;
    [DataMember(Name = "Type")]
    private readonly string type;
#endif

    /// <summary>
    /// Adds a <paramref name="value"/> under <paramref name="key"/> to <see cref="Data"/>.  If either the key
    /// or the value is null, nothing is added to the data dictionary.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// A would-be inserted key already exists in <see cref="Data"/>.
    /// </exception>
    public void Add(string key, object value) {
        if (key is null || value is null)
            return;

        var t = value.GetType();
        if (!KnownTypes.Contains(t)) {
            if (t.IsEnum) {
                value = new UntypedValue(t.FullName, Enum.Format(t, value, "g"));
            } else {
                value = new UntypedValue(t.FullName, (string)ToStringMethod.Invoke(value, null));
            }
        }

        if (_Data == null)
            _Data = new(8);
        _Data.Add(key, value);   // Use property to create it on demand.
    }

    /// <summary>
    /// Adds public instance data members from <paramref name="o"/> to <see cref="Data"/>.  For compactness, <c>null</c>
    /// values are omitted.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The key for each member is constructed by concatenating the fully qualified name of the declaring type
    /// (to take inheritance into account) with the member name.  The type of "simple" values is preserved, other
    /// values are converted to strings.
    /// </para>
    /// <para>
    /// This method uses reflection, so it should not be used in performance-critical scenarios.
    /// </para>
    /// </remarks>
    /// <param name="o">Object instance that provides data.</param>
    /// <param name="filter">
    /// Delegate that receives a field or property info about a public field or property on <paramref name="o"/>
    /// and returns true if the member should be included in <see cref="Data"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// A would-be inserted key already exists in <see cref="Data"/>.
    /// </exception>
    protected void AddMembers(object o, Func<MemberInfo, bool> filter = null) {
        var info = GetReflectedMembers(o, filter);
        foreach (var mi in info) {
            var k = string.Format("{0}.{1}", mi.DeclaringType.FullName, mi.Name);
            var v = GetMemberValue(mi);
            Add(k, v);
        }

        object GetMemberValue(MemberInfo mi) {
            switch (mi) {
            case PropertyInfo pi: return pi.GetValue(o);
            case FieldInfo fi: return fi.GetValue(o);
            }
            throw new NotSupportedException("Invalid member type: " + mi.GetType().FullName);
        }
    }

    MemberInfo[] GetReflectedMembers(object o, Func<MemberInfo, bool> filter) {
        var t = o.GetType();
        var p = t.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(x => x.CanRead).Select(x => (MemberInfo)x);
        var f = t.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(x => (MemberInfo)x);
        return p.Concat(f).Where(EvaluateFilter).ToArray();
        bool EvaluateFilter(MemberInfo mi) => filter?.Invoke(mi) ?? true;
    }

    /// <summary>
    /// Convenience overload for the most common use of <see cref="AddMembers(object, Func{MemberInfo, bool})"/>:
    /// adding all of public fields and/or properties to the bag.
    /// </summary>
    /// <param name="o">Object instance.  May be null, in which case the call is a no-op.</param>
    /// <param name="include">
    /// A bitmask determining whether to include properties, fields or both.  Default is only properties.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="include"/> contains invalid bits.
    /// </exception>
    public void AddMembers(object o, int include = IncludeProperties) {
        if (o is null)
            return;
        if (include < 0 || include > 3)
            throw new ArgumentOutOfRangeException(nameof(include));
        AddMembers(o, ShouldInclude);

        bool ShouldInclude(MemberInfo mi) {
            switch (mi) {
            case PropertyInfo _: return (include & IncludeProperties) != 0;
            case FieldInfo _: return (include & IncludeFields) != 0;
            }
            return false;
        }
    }

    private static readonly MethodInfo ToStringMethod = typeof(object).GetMethod("ToString");

    private static IEnumerable<Type> GetKnownTypes() => KnownTypes;

    private protected static readonly HashSet<Type> KnownTypes = new HashSet<Type>() {
        typeof(bool),
        typeof(sbyte), typeof(short), typeof(int), typeof(long),
        typeof(byte), typeof(ushort), typeof(uint), typeof(ulong),
        typeof(decimal), typeof(double), typeof(float),
        typeof(string), typeof(char),
        typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan), typeof(Guid),
        typeof(byte[]),
        typeof(UntypedValue),
        typeof(PropertyValueBag),
        //typeof(ObjectPropertyBag),
        //typeof(ExceptionPropertyBag)
    };
}
