using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Quine.Schemas.Graph;
using Autofac;
using Quine.HRCatalog;

namespace Quine.Graph;

/// <summary>
/// Container and controller for nodes.
/// </summary>
/// <remarks>
/// Incrementally constructed graph should not be considered runnable because ctors of nodes may
/// use introspection (e.g., finding out the number of successor nodes).  In these cases, introspection
/// will return wrong results and the graph will not run properly.  The state-based constructor
/// must ALWAYS be used to run the graph.
/// </remarks>
public sealed class GraphShell : GraphSchemaHook<GraphState>
{
    private readonly ILifetimeScope lifetimeScope;
    
    private readonly Dictionary<Guid, InteractiveQueryEventData> queries = new();
    private readonly List<NodeShell> nodes = new List<NodeShell>();
    private readonly CancellationTokenSource cts;

    /// <summary>
    /// This property is available only while the graph is running.
    /// </summary>
    internal CancellationToken CancellationToken => QHEnsure.NotNull(cts).Token;

    #region Construction and disposal

    /// <summary>
    /// Constructor.  The graph cannot be run before <see cref="Build"/> has been invoked.
    /// </summary>
    /// <param name="graphState">Description of the graph to run.</param>
    /// <param name="owner">The "owning" job in the tree; use <c>null</c> for root jobs.</param>
    /// <param name="parent">Lifetime scope to use for the run.  The graph and all nodes are constructed with it as the initial lifetime.</param>
    public GraphShell
        (
        ILifetimeScope parent,
        ITreeIdentity owner,
        GraphState graphState
        )
        : base(owner, graphState)
    {
        // NB: Even w/o anything to register, lifetime scope is needed to track disposables within the graph.
        this.lifetimeScope = parent;
        this.cts = new CancellationTokenSource();   // Needed for Build()
        this.State.Trace = new(GetType().FullName + "/" + PathId);
    }

    /// <summary>
    /// This method must be invoked before starting the graph.  For technical reasons, two-phase initialization is necessary.
    /// </summary>
    public void Build() {
        foreach (var n in State.Nodes)
            nodes.Add(CreateNode(n));
        foreach (var portState in nodes.SelectMany(n => n.State.OutputPorts))
            ((IOutputPort)portState.RuntimeObject).Connect();
        foreach (var n in nodes)
            ValidateNodeConnections(n);

        NodeShell CreateNode<T>(T state) where T : NodeStateBase {
            var t = Type.GetType(state.ImplementingType, true);   // Causes loading of the assembly, throws on errors.
            return (NodeShell)lifetimeScope.Resolve(t, new TypedParameter(state.GetType(), state));
        }

        static void ValidateNodeConnections(NodeShell node) {
            foreach (var ips in node.State.InputPorts)
                QHEnsure.State(((IInputPort)ips.RuntimeObject).Predecessors.Count > 0);
            foreach (var ops in node.State.OutputPorts)
                QHEnsure.State(((IOutputPort)ops.RuntimeObject).Successors.Count() > 0);
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired on graph/node run state change.
    /// </summary>
    public event Action<ITreeIdentity, GraphRunState> RunState;

    /// <summary>
    /// Fired on every trace message.
    /// </summary>
    public event Action<ITreeIdentity, GraphMessage, Schemas.Core.Eventing.OperationalEvent> ShellTrace;

    /// <summary>
    /// Fired when a node starts processing a message, ends processing, or reports progress.
    /// </summary>
    public event Action<ITreeIdentity, ItemProcessingEventData> ItemProcessing;

    /// <summary>
    /// Fired when a node is requesting interactive input.
    /// </summary>
    public event Action<ITreeIdentity, InteractiveQueryEventData> InteractiveQuery;

    private void ClearEventSubscriptions() {
        RunState = null;
        ShellTrace = null;
        ItemProcessing = null;
        InteractiveQuery = null;
    }

    internal void Publish(ITreeIdentity sender, GraphRunState runState) {
        try {
            RunState?.Invoke(sender, runState);
        }
        catch (Exception e) {
            Console.WriteLine($"Event handler threw.\n{e}");
        }
    }

    internal void Publish(ITreeIdentity sender, GraphMessage m, Schemas.Core.Eventing.OperationalEvent eventData) {
        try {
            if (sender == this && !State.Trace.IsDisposed)
                State.Trace.Add(eventData);
            ShellTrace?.Invoke(sender, m, eventData);
        }
        catch (Exception e) {
            Console.WriteLine($"Event handler threw.\n{e}");
        }
    }

    internal void Publish(ITreeIdentity sender, ItemProcessingEventData eventData) {
        try {
            ItemProcessing?.Invoke(sender, eventData);
        }
        catch (Exception e) {
            Console.WriteLine($"Event handler threw.\n{e}");
        }
    }

    internal Task<object> QueryAsync(ITreeIdentity sender, object query) {
        var iqd = new InteractiveQueryEventData(query, CancellationToken);
        lock (queries)
            queries.Add(iqd.Id, iqd);
        iqd.Continuation = iqd.Reply.Task.ContinueWith(QueryContinuation);
        InteractiveQuery?.Invoke(sender, iqd);
        return iqd.Continuation;

        object QueryContinuation(Task<object> t) {
            lock (queries)
                queries.Remove(iqd.Id);
            if (t.IsCompletedSuccessfully)
                return t.Result;
            if (t.IsFaulted)
                throw t.Exception;
            if (t.IsCanceled)
                throw new OperationCanceledException(CancellationToken);
            throw new InvalidOperationException($"Invalid task state: {t.Status}");
        }
    }

    #endregion

    #region Master methods for starting/unblocking/cancelling the execution.

    // Valid values: 0=not started, 1=started&running, 2=cancelled, 3=completed.
    private volatile int _runState = 0;

    private const int RS_Ready = 0;
    private const int RS_Running = 1;
    private const int RS_Cancelled = 2;
    private const int RS_Completed = 3;
    
    public IReadOnlyCollection<NodeShell> Nodes => nodes;

    /// <summary>
    /// Starts an asynchronous run of the graph.
    /// When completed, all state is finalized and won't be changed asynchronously.
    /// This method can be invoked only once.
    /// </summary>
    public async Task RunAsync() {
        QHEnsure.State(Interlocked.Exchange(ref _runState, RS_Running) == RS_Ready);

        Exception runexn = null;
        try {
            Publish(this, GraphRunState.Running);
            Publish(this, null, new QHNotificationEvent(QHGraph.I_GraphStarting, null, Id));
            foreach (var n in nodes)
                n.StartAsync();
            await Task.WhenAll(nodes.Select(n => n.Task));
        }
        catch (Exception e) {
            runexn = e;
        }
        finally {
            Interlocked.Exchange(ref _runState, RS_Completed);
            cts.Dispose();
        }

        try {
            State.CompletionState = (GraphRunState)nodes.Select(n => (int)n.State.CompletionState).Max();
            Publish(this, null, new QHNotificationEvent(QHGraph.I_GraphStopped, runexn, Id));
            Publish(this, State.CompletionState);
        }
        catch {
            State.CompletionState = GraphRunState.Failed;
        }
        finally {
            State.Trace.Dispose();
            ClearEventSubscriptions();
        }
    }

    /// <summary>
    /// Cancels graph execution.  This method is a no-op if the graph is not currently running (not started or completed).
    /// </summary>
    public void Cancel() {
        if (Interlocked.CompareExchange(ref _runState, RS_Cancelled, RS_Running) == RS_Running) {   // Still running.
            lock (queries) {
                foreach (var q in queries) {
                    q.Value.Reply.TrySetCanceled(CancellationToken);
                }
            }
            cts.Cancel();
        }
    }

    #endregion
}
