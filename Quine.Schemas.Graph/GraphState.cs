using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

using Quine.HRCatalog;

namespace Quine.Schemas.Graph;

/// <summary>
/// Reflects the completion state of the graph or the node.
/// </summary>
[DataContract(Namespace = XmlNamespaces.Graph)]
public enum GraphRunState
{
    /// <summary>
    /// The graph is running.
    /// </summary>
    [EnumMember] Running,

    /// <summary>
    /// The graph has completed successfully.
    /// </summary>
    [EnumMember] Completed,

    /// <summary>
    /// The graph has been cancelled.
    /// </summary>
    [EnumMember] Canceled,

    /// <summary>
    /// At least one node ended up in "error" state.
    /// </summary>
    [EnumMember] Error,

    /// <summary>
    /// At least one node terminated with an unhandled exception. (I.e., an exception that propagated to the "outermost" handler.)
    /// </summary>
    [EnumMember] Failed
}

/// <summary>
/// Class for serializing the complete state of the graph, including active messages and their histories.
/// </summary>
[DataContract(Namespace = XmlNamespaces.Graph)]
public class GraphState : GraphRuntimeHook
{
    /// <summary>
    /// Collection of nodes in the graph.
    /// </summary>
    [DataMember]
    public List<NodeStateBase> Nodes { get; private set; } = new();

    /// <summary>
    /// Trace events generated during execution.
    /// </summary>
    [DataMember]
    public Schemas.Core.Eventing.OperationalTrace Trace { get; internal set; }

    /// <summary>
    /// The graph's completion state.
    /// </summary>
    [DataMember]
    public GraphRunState CompletionState { get; set; }

    /// <summary>
    /// Makes sure that node ids are assigned sequentially starting from <c>id+1</c>.
    /// </summary>
    public override void SetId(ITreeIdentity owner, int id) {
        base.SetId(owner, id);
        for (int i = 0; i < Nodes.Count; ++i)
            Nodes[i].SetId(this, id + 1 + i);
    }
}
