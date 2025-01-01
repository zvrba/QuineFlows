using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Autofac;

using Quine.HRCatalog;
using Quine.Schemas.Graph;

namespace Quine.Graph;

public abstract class NodeShell<T> : NodeShell where T : NodeStateBase
{
    protected NodeShell(ILifetimeScope lifetimeScope, GraphShell owner, T state) : base(lifetimeScope, owner, state) { }
    public new T State => (T)base.State;
}

/// <summary>
/// Base functionality shared by all node implementations.  New node types can be implemented outside of
/// this assembly only by deriving from one of the generic node classes.
/// </summary>
public abstract class NodeShell : GraphSchemaHook<NodeStateBase>, INodeEventSource
{
    private readonly GraphShell owner;

    private Task task;
    
    protected readonly ILifetimeScope _LifetimeScope;
    protected readonly string _TraceSource;
    
    #region Construction and disposal

    /// <summary>
    /// Constructs node from deserialized state together with owned input and output ports.
    /// No connections are created because target nodes may not have been created yet.
    /// NB: No messages can be logged to the shell from constructors because it's too early
    /// to subscribe to the events.  Any services that can fail should be resolved / initialized
    /// from (overridden) <see cref="LifecycleAsync"/>.
    /// </summary>
    protected NodeShell
        (
        ILifetimeScope lifetimeScope,
        GraphShell owner,
        NodeStateBase state
        ) : base(owner, state)
    {
#if false   // Does not play nice with changing assembly versions. Old workflow references old assembly version
        if (state.ImplementingType != GetType().AssemblyQualifiedName)
            throw new ArgumentException($"Runtime node class ({GetType().AssemblyQualifiedName}) inconsistent with node state class ({state.ImplementingType}).");
#endif

        this.owner = owner;
        this._LifetimeScope = lifetimeScope;
        this._TraceSource = $"{GetType().FullName}/{Id}";
        this.State.Trace = new(GetType().FullName + "/" + PathId);
        CancellationToken = owner.CancellationToken;
    }

    /// <summary>
    /// Determines how many instances of this node type can run concurrently.
    /// Use <c>int.MaxValue</c> for unbounded number of instances.
    /// </summary>
    internal protected abstract int ConcurrencyLimit { get; }

    #endregion

    #region Events

    CancellationToken INodeEventSource.CancellationToken => CancellationToken;
    
    ItemProcessingEventData INodeEventSource.ItemProcessingEventData => ItemProcessingEventData;
    
    void INodeEventSource.RaiseProgressEvent(long? processedSize, long? sizeIncrement, float? progress) =>
        RaiseProgressEvent(processedSize, sizeIncrement, progress);
    
    void INodeEventSource.RaiseTraceEvent(Schemas.Core.Eventing.OperationalEvent @event) =>
        RaiseTraceEvent(@event);

    /// <summary>
    /// Information about the item currently being processed.
    /// </summary>
    /// <seealso cref="RaiseProcessingBeginEvent(GraphMessage)"/>
    /// <seealso  cref="RaiseProcessingEndEvent(MessageProcessingState, Exception)"/>
    /// <seealso cref="RaiseProgressEvent(long?, long?, float?)"/>
    protected ItemProcessingEventData ItemProcessingEventData { get; private set; }

    // TODO: DEFINE EVENTS FOR PROCESSING BEGIN/END TO APPEND TO TRACE

    // Note: use-case for overriding Raise* methods is to avoid subscribing on self.

    /// <summary>
    /// Overriding this method allows to create derived instances of <see cref="ItemProcessingEventData"/>
    /// for reporting of extended progress events.
    /// </summary>
    protected virtual ItemProcessingEventData CreateItemProcessingEventData(GraphMessage m, string source) => new(m, source);

    /// <summary>
    /// Sets current message to <paramref name="m"/> and singals start of processing.
    /// If overridden, the base implementation MUST be called.
    /// </summary>
    protected virtual void RaiseProcessingBeginEvent(GraphMessage m) {
        ItemProcessingEventData = CreateItemProcessingEventData(m, _TraceSource);
        ItemProcessingEventData.SetState(MessageProcessingState.Accepted, null);
        owner.Publish(this, ItemProcessingEventData);
    }

    /// <summary>
    /// Signals end of processing for the message set by <see cref="RaiseProcessingBeginEvent(GraphMessage)"/>
    /// and sets the current message to <c>null</c>.
    /// If overridden, the base implementation MUST be called.
    /// </summary>
    protected virtual void RaiseProcessingEndEvent(MessageProcessingState state, Exception exn) {
        try {
            ItemProcessingEventData.SetState(state, exn);
            ItemProcessingEventData.Trace.Dispose();
            if (state == MessageProcessingState.Failed || exn != null)
                State.CompletionState = GraphRunState.Error;
            owner.Publish(this, ItemProcessingEventData);
        }
        finally {
            ItemProcessingEventData = null;
        }
    }

    /// <summary>
    /// Raises progress event for the message set by <see cref="RaiseProcessingBeginEvent(GraphMessage)"/>.
    /// At most one of the arguments can be set to a non-negative value.
    /// If overridden, the base implementation MUST be called.
    /// </summary>
    /// <param name="processedSize">Set to report current absolute size of processed data.</param>
    /// <param name="sizeIncrement">Set to report incremental size of processed data.</param>
    /// <param name="progress">Set to report current absolute progress as fraction between 0 and 1.</param>
    protected virtual void RaiseProgressEvent
        (
        long? processedSize = null,
        long? sizeIncrement = null,
        float? progress = null
        )
    {
        if (processedSize.HasValue)
            ItemProcessingEventData.SetProcessedSize(processedSize.Value);
        else if (sizeIncrement.HasValue)
            ItemProcessingEventData.IncrementProcessedSize(sizeIncrement.Value);
        else if (progress.HasValue)
            ItemProcessingEventData.SetItemProgress(progress.Value);
        if (ItemProcessingEventData.ShouldPublish())
            owner.Publish(this, ItemProcessingEventData);
    }

    /// <summary>
    /// Sends interactive query to the controlling front-end.
    /// </summary>
    /// <param name="query">
    /// Query object to send.  Must be serializable.
    /// </param>
    /// <returns>
    /// Query reply.
    /// </returns>
    protected virtual Task<object> QueryAsync(object query) => owner.QueryAsync(this, query);

    /// <summary>
    /// Raises a trace event.  The event is always published to the shell, and then appended to either the item's
    /// trace (if <see cref="ItemProcessingEventData"/> is not null) or to the node's trace if the lifecycle allows it.
    /// </summary>
    /// <param name="event">Event to raise.</param>
    protected virtual void RaiseTraceEvent(Schemas.Core.Eventing.OperationalEvent @event)
    {
        if (ItemProcessingEventData != null)
            ItemProcessingEventData.Trace.Add(@event);
        else if (State.Trace != null && !State.Trace.IsDisposed)
            State.Trace.Add(@event);
        
        if (@event.EventId.IsHighOrError)
            State.CompletionState = GraphRunState.Error;
        
        owner.Publish(this, ItemProcessingEventData?.GraphMessage, @event);
    }

    #endregion

    private async Task OuterRunExceptionWrapper() {
        var ncl = _LifetimeScope.Resolve<NodeConcurrencyLimiter>();
        await ncl.WaitAsync(this);
        
        try {                       // Past successful wait, Release() must be eventually called.
            try {
                await LifecycleAsync();
            }
            catch(Exception e) {    // "Everything" failed, can't use EventSource, etc.  Log only to shell.
                State.CompletionState = GraphRunState.Failed;
                RaiseTraceEvent(new QHNotificationEvent(QHGraph.C_UnhandledException, e, _TraceSource));
            }
            finally {
                foreach (var op in GetPorts<IOutputPort>(State.OutputPorts))
                    op.Close();
                foreach (var ip in GetPorts<IInputPort>(State.InputPorts))
                    ip.ClearEventSubscriptions();
                State.Trace.Dispose();
            }
        }
        finally {
            ncl.Release(this);      // Really critical to run under any failure above.
        }
    }

    #region For use by concrete nodes

    /// <summary>
    /// This method defines the complete node lifecycle. Overriding this method allows the node to perform actions before
    /// <see cref="MessageLoopAsync"/> has been started and after it has exited.  Notes to implementers: The derived
    /// implementation MUST call the base implementation.  Most of the protected methods cannot be called before or
    /// after this method has executed.
    /// </summary>
    protected virtual async Task LifecycleAsync() {
        owner.Publish(this, GraphRunState.Running);
        
        try {
            QHEnsure.State(State.CompletionState == GraphRunState.Running);
            RaiseTraceEvent(new QHNotificationEvent(QHGraph.I_NodeStarting, null, _TraceSource));
            await MessageLoopAsync();
            RaiseTraceEvent(new QHNotificationEvent(QHGraph.I_NodeStopped, null, _TraceSource));
        }
        catch (OperationCanceledException) {
            State.CompletionState = GraphRunState.Canceled;
            RaiseTraceEvent(new QHNotificationEvent(QHGraph.I_Node_Canceled, null, _TraceSource));
        }
        catch (ThreadInterruptedException) {
            State.CompletionState = GraphRunState.Canceled;
            RaiseTraceEvent(new QHNotificationEvent(QHGraph.I_Node_Interruption, null, _TraceSource));
        }
        catch (ChannelClosedException) {
            RaiseTraceEvent(new QHNotificationEvent(QHGraph.I_NodeCompleted, null, _TraceSource));
        }
        catch (Exception e) {   // Unhandled exception from MessageLoppAsync
            State.CompletionState = GraphRunState.Failed;
            RaiseTraceEvent(new QHNotificationEvent(QHGraph.C_UnhandledException, e, _TraceSource));
        }

        // OnProcessingEnd subscribed to by ctor sets state to Error if any item fails.
        if (State.CompletionState == GraphRunState.Running)
            State.CompletionState = GraphRunState.Completed;

        owner.Publish(this, State.CompletionState);
    }

    private static IEnumerable<TPort> GetPorts<TPort>(PortStateBase[] ports) {
        if (ports == null)
            return Enumerable.Empty<TPort>();
        return ports.Select(x => (TPort)x.RuntimeObject);
    }


    /// <summary>
    /// The actual message processing loop for the node.
    /// </summary>
    protected abstract Task MessageLoopAsync();

    /// <summary>
    /// Used to signal that the graph is being canceled; shared by all nodes.
    /// </summary>
    protected CancellationToken CancellationToken { get; }

#endregion

#region Internal methods

    internal Task Task => task;
    
    internal void StartAsync() {
        QHEnsure.State(task == null);
        task = Task.Factory.StartNew(OuterRunExceptionWrapper, CancellationToken).Unwrap();
    }

#endregion
}
