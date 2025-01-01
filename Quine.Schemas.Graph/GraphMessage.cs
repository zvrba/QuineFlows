using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Quine.Schemas.Graph;

/// <summary>
/// Processing outcome of a graph message.
/// </summary>
[DataContract(Namespace = XmlNamespaces.Graph)]
public enum MessageProcessingState
{
    /// <summary>
    /// The message resides in the input port of a node.
    /// </summary>
    [EnumMember] Queued,

    /// <summary>
    /// The node has started to process the message.
    /// </summary>
    [EnumMember] Accepted,

    /// <summary>
    /// Message processing completed sucessfully.
    /// </summary>
    [EnumMember] Completed,

    /// <summary>
    /// Message processing failed.
    /// </summary>
    [EnumMember] Failed
}

/// <summary>
/// Messages flowing through the graph must derive from this class.
/// </summary>
[DataContract(Namespace = XmlNamespaces.Graph, IsReference = true)]
[KnownType("GetKnownTypes")]
public abstract class GraphMessage : Core.IIdentity<Guid>
{
    /// <summary>
    /// Derived classes must be registered in this set.
    /// </summary>
    protected static readonly HashSet<Type> KnownTypes = new HashSet<Type>();
    private static IEnumerable<Type> GetKnownTypes() => KnownTypes;

    [DataMember(Name = "Id")]
    private readonly Guid id = Guid.NewGuid();

    /// <summary>
    /// The unique id of this message.
    /// </summary>
    public Guid Id { get { return id; } }
}
