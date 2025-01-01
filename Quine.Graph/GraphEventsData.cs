using System;
using System.Threading;
using System.Threading.Tasks;

using Quine.HRCatalog;
using Quine.Schemas.Graph;

namespace Quine.Graph;

// TODO: Enqueue events.

/// <summary>
/// Event data for an interactive query event.  <see cref="Query"/> contains the query object which must be completed
/// with one of <see cref="SetResult(object)"/>, <see cref="SetError(Exception)"/> or <see cref="Cancel"/> methods.
/// </summary>
public sealed class InteractiveQueryEventData
{
    internal InteractiveQueryEventData(object query, CancellationToken cancellationToken) {
        Query = QHEnsure.NotNull(query);
        Reply = new(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken = cancellationToken;
    }

    internal readonly Guid Id = Guid.NewGuid();

    public readonly object Query;
    public void SetResult(object result) => Reply.SetResult(result);
    public void Cancel() => Reply.SetCanceled(CancellationToken);
    public void SetError(Exception exception) => Reply.SetException(exception);

    internal readonly TaskCompletionSource<object> Reply;
    internal readonly CancellationToken CancellationToken;
    internal Task<object> Continuation;
}

/// <summary>
/// Event data common for begin/progress/end processing events.
/// This is a stateful class where it is intended that every node has a single instance of a subclass implementing
/// <see cref="GraphMessageTotalSize"/> in a suitable way.
/// Implementation note: This data cannot be part of <c>GraphMessage</c> because a single message can be queued at
/// and processed by multiple nodes.
/// </summary>
public class ItemProcessingEventData
{
    /// <summary>
    /// To avoid message flooding, progress events are sent not more frequently than this period (unless forced).
    /// </summary>
    public static readonly TimeSpan ProgressReportingPeriod = TimeSpan.FromSeconds(1);

    private long processedSize;
    private DateTime lastPublished;
    private bool shouldPublish;

    internal protected ItemProcessingEventData(GraphMessage item, string source)
    {
        this.GraphMessage = item;
        this.lastPublished = DateTime.Now - ProgressReportingPeriod * 2;    // Force publish on first progress update
        Trace = new(source);   // NB! Disposed by NodeShell.RaiseProcessingEndEvent
        if (GraphMessageTotalSize > 0)
            Progress = 0;
    }

    public GraphMessage GraphMessage { get; }
    public MessageProcessingState MessageProcessingState { get; private set; }
    public float? Progress { get; private set; }
    public Exception Exception { get; private set; }

    /// <summary>
    /// Returns the total absolute "size" of <see cref="GraphMessage"/> for progress computation.
    /// If the value is 0 or negative, tracking of progress based on processed size is unavailable,
    /// but the progress can still be explicitly set by <see cref="SetItemProgress(float)"/>.
    /// NB! This property is virtually invoked from ctor and the implementation may inspect only
    /// </summary>
    public virtual long GraphMessageTotalSize => 0;
    
    /// <summary>
    /// Only snapshot is observable during processing.  The whole trace is available only after processing end is signaled.
    /// </summary>
    public Schemas.Core.Eventing.OperationalTrace Trace { get; }

    // NB! This is a method instead of property because automatic property display in the debugger would reset the value.
    internal bool ShouldPublish() {
        var ret = shouldPublish;
        shouldPublish = false;
        return ret;
    }

    public void SetState(MessageProcessingState state, Exception exception) {
        MessageProcessingState = state;
        Exception = exception;
        shouldPublish = true;
    }

    /// <summary>
    /// Sets absolute processed size to <paramref name="s"/>, ensuring monotonicity of <see cref="Progress"/>.
    /// This method can be called only when <see cref="GraphMessageTotalSize"/> is positive.
    /// </summary>
    public void SetProcessedSize(long s) {
        QHEnsure.State(GraphMessageTotalSize > 0);
        if (s < processedSize) s = processedSize;
        if (s > GraphMessageTotalSize) s = GraphMessageTotalSize;
        processedSize = s;
        SetItemProgress((float)processedSize / GraphMessageTotalSize);
    }

    /// <summary>
    /// Increments absolute processed size by <paramref name="s"/>, ensuring monotonicity of <see cref="Progress"/>.
    /// The operation is atomic, i.e., may be called by multiple threads without additional synchronization.
    /// This method can be called only when <see cref="GraphMessageTotalSize"/> is positive.
    /// </summary>
    public void IncrementProcessedSize(long s) {
        QHEnsure.State(GraphMessageTotalSize > 0 && s > 0);
        if (Interlocked.Add(ref processedSize, s) >= GraphMessageTotalSize)
            processedSize = GraphMessageTotalSize;
        SetItemProgress((float)processedSize / GraphMessageTotalSize);
    }

    /// <summary>
    /// Sets <see cref="Progress"/> to <paramref name="progress"/>.  This method can be always called.
    /// This method also triggers event publishing.  Additional instance data in derived classes must be set before invoking this method.
    /// </summary>
    /// <param name="progress">
    /// Absolute progress value to set. Negative values set <see cref="Progress"/> to null (unknown), whereas values larger than 1
    /// are clamped to 1.
    /// </param>
    public void SetItemProgress(float progress) {
        Progress = progress switch {
            < 0 => null,
            > 1 => 1,
            _ => progress
        };
        SetShouldPublish();
    }

    /// <summary>
    /// Should be invoked by the derived class when additional data is ready for publishing.
    /// </summary>
    /// <param name="force">
    /// If true, the data is published as soon as possible instead after at least <see cref="ProgressReportingPeriod"/> has elapsed
    /// since the last publish.
    /// </param>
    protected void SetShouldPublish(bool force = false) {
        var now = DateTime.Now;
        if (Progress == 1 || now - lastPublished > ProgressReportingPeriod)
            force = true;
        if (force) {
            shouldPublish = true;
            lastPublished = now;
        }
    }
}
