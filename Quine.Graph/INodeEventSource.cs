using System;
using System.Threading;

namespace Quine.Graph;

/// <summary>
/// This interface allows components that run as a part of a node to publish events.
/// </summary>
public interface INodeEventSource
{
    /// <summary>
    /// Signaled when the node has been cancelled.
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Item currently being processed.
    /// </summary>
    ItemProcessingEventData ItemProcessingEventData { get; }

    /// <summary>
    /// Raises a progress event for the item currently being processed (available in <see cref="ItemProcessingEventData"/>).
    /// <see cref="NodeShell.RaiseProgressEvent(long?, long?, float?)"/> for details.
    /// </summary>
    void RaiseProgressEvent
        (
        long? processedSize = null,
        long? sizeIncrement = null,
        float? progress = null
        );

    /// <summary>
    /// Raises a trace event.
    /// <see cref="NodeShell.RaiseTraceEvent(Schemas.Core.Eventing.OperationalEvent, EventTarget)"/> for details.
    /// </summary>
    void RaiseTraceEvent(Schemas.Core.Eventing.OperationalEvent @event);
}

