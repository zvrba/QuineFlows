using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Quine.HRCatalog;

namespace Quine.Schemas.Core.Eventing;

/// <summary>
/// Provides a factory method for creating (subclasses of) <see cref="ExceptionPropertyBag"/>.  Exceptions
/// implementing this interface is the preferred method of serializing structured exception data (instead
/// of populating <see cref="PropertyValueBag.Data"/>).
/// </summary>
public interface IExceptionPropertyBagProvider
{
    /// <summary>
    /// Creates a (derived) instance of <see cref="ExceptionPropertyBag"/> with additional serializable data.
    /// </summary>
    /// <param name="nestingLevel">The exception's nesting level; passed to the constructor.</param>
    /// <returns>A valid instance.</returns>
    ExceptionPropertyBag CreateExceptionPropertyBag(int nestingLevel);
}

/// <summary>
/// Data from <see cref="Exception"/> suitable for serialization.  Use <see cref="Create(Exception)"/> method
/// to create an instance.  If the exception implements <see cref="IExceptionPropertyBagProvider"/>, the interface
/// is used to create a (derived) instance of <see cref="ExceptionPropertyBag"/>.  Otherwise, the exception's
/// public properties are added to <see cref="PropertyValueBag.Data"/> dictionary.
/// </summary>
[DataContract(Namespace = XmlNamespaces.Core_1_0)]
public class ExceptionPropertyBag : PropertyValueBag
{
    /// <summary>
    /// The actual exception that this instance describes.
    /// </summary>
    public readonly Exception Exception;

    /// <summary>
    /// The exception type.
    /// </summary>
    public string ExceptionType => exceptionType;
    [DataMember(Name = "ExceptionType")]
    private readonly string exceptionType;

    /// <summary>
    /// Parsed from the exception object.  Includes full type name and method name.
    /// </summary>
    public string TargetSite => targetsite;
    [DataMember(Name = "TargetSite")]
    private readonly string targetsite;

    /// <summary>
    /// From the exception object.
    /// </summary>
    public string Message => message;
    [DataMember(Name = "Message")]
    private readonly string message;

    /// <summary>
    /// From the exception.
    /// </summary>
    public int HResult => hresult;
    [DataMember(Name = "HResult")]
    private readonly int hresult;

    /// <summary>
    /// From the exception.
    /// </summary>
    public string Source => source;
    [DataMember(Name = "Source")]
    private readonly string source;

    /// <summary>
    /// List of inner exceptions.  This is either null or non-empty.
    /// </summary>
    public IReadOnlyList<ExceptionPropertyBag> InnerExceptions => inner;
    [DataMember(Name = "InnerExceptions")]
    private readonly ExceptionPropertyBag[] inner;

    // Order attributes below are necessary; serialized data already exists.

    /// <summary>
    /// The exception's nesting level.  The root exception has level 0.
    /// </summary>
    [DataMember(Order = 1)] 
    public readonly int NestingLevel;

    static readonly HashSet<string> IgnoredProperties = new HashSet<string>() {
        "Data", "Message", "TargetSite", "InnerException", "InnerExceptions", "HResult", "Source"
    };

    /// <summary>
    /// Constrcutor.
    /// </summary>
    /// <param name="exn">The exception to serialize.</param>
    /// <param name="nestingLevel">The exception's nesting level.</param>
    protected ExceptionPropertyBag(Exception exn, int nestingLevel) {
        Exception = QHEnsure.NotNull(exn);
        NestingLevel = QHEnsure.Value(nestingLevel, nestingLevel >= 0);

        exceptionType = exn.GetType().FullName;
        
        try { targetsite = string.Format("{0}.{1}", exn.TargetSite.DeclaringType.FullName, exn.TargetSite.Name); }
        catch (NullReferenceException) { targetsite = "(No site.)"; }   // When the exception has not been thrown.

        hresult = exn.HResult;
        source = exn.Source;
        message = exn is AggregateException ? "(AggregateException)" : exn.Message;

        if (exn is not IExceptionPropertyBagProvider)
            AddMembers(exn, mi => mi is System.Reflection.PropertyInfo && !IgnoredProperties.Contains(mi.Name));

        if (exn is AggregateException ae && ae.InnerExceptions?.Count > 0)
            inner = ae.InnerExceptions.Select(x => Create(x, nestingLevel + 1)).ToArray();
        else if (exn.InnerException != null)
            inner = new ExceptionPropertyBag[] { Create(exn.InnerException, nestingLevel + 1) };
    }

    private static ExceptionPropertyBag Create(Exception exn, int nestingLevel) =>
        exn is IExceptionPropertyBagProvider esd ? esd.CreateExceptionPropertyBag(nestingLevel) :
        new ExceptionPropertyBag(exn, nestingLevel);

    /// <summary>
    /// Creates an instance of <see cref="ExceptionPropertyBag"/>.
    /// </summary>
    public static ExceptionPropertyBag Create(Exception exn) => Create(exn, 0);

    /// <summary>
    /// Flattens <c>this</c> into a DFS-ordered list.  The list contains the same instances, with preserved
    /// inner exceptions.
    /// </summary>
    public ExceptionPropertyBag[] Flatten() {
        var flat = new List<ExceptionPropertyBag>();
        Flatten(this, 0, flat);
        return flat.ToArray();

        static void Flatten(ExceptionPropertyBag bag, int nestingLevel, List<ExceptionPropertyBag> flat) {
            flat.Add(bag);
            if (bag.InnerExceptions?.Count > 0)
                foreach (var e in bag.InnerExceptions)
                    Flatten(e, nestingLevel + 1, flat);
        }
    }
}
