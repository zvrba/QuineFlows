using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading.Tasks;

using Autofac;

using Quine.Graph;
using Quine.Schemas.Graph;

namespace Quine.Samples;

internal class GraphSample : IDisposable, ITreeIdentity
{
    #region Instantiate and run

    public static async Task ExecuteAsync(int increment, int count, int constant) {
        // Build using command-line arguments
        using var self = new GraphSample();
        self.increment = increment;
        self.count = count;
        self.constant = constant;
        await self.RunAsync();
    }

    #endregion

    private readonly IContainer container;
    private int increment, count, constant;

    #region Run the graph

    private async Task RunAsync()
    {
        // Create state and set the graph's ID within the parent.
        var graphState = BuildGraph();
        graphState.SetId(this, 1);

        // Create a scope for the graph run and register the state instance and "owner".
        // Remember that GraphShell MUST be registered with per lifetime scope.
        using (var scope = container.BeginLifetimeScope(cb => {
            cb.RegisterInstance(graphState);
            cb.RegisterInstance(this).As<ITreeIdentity>();
        })) {
            var graphShell = scope.Resolve<GraphShell>();
            graphShell.Build(); // IMPORTANT!
            await graphShell.RunAsync();
            Console.WriteLine($"FINISHED, EXIT STATUS WAS: {graphShell.State.CompletionState}");
        }
    }

    #endregion

    #region Implement ITreeIdentity

    ITreeIdentity ITreeIdentity.Owner => null;
    TreePathId ITreeIdentity.PathId => default;
    int Schemas.Core.IIdentity<int>.Id => 0;

    #endregion

    #region Build Autofac container with node behaviors

    private GraphSample() {
        var cb = new ContainerBuilder();

        // Node behaviors.
        cb.RegisterType<CustomSourceNode>().AsSelf();
        cb.RegisterType<CustomTransformNode>().AsSelf();
        cb.RegisterType<CustomDrainNode>().AsSelf();

        // Always needed by the framework.  A scope MUST be created for each graph run.
        cb.RegisterType<GraphShell>().InstancePerLifetimeScope();
        cb.RegisterType<NodeConcurrencyLimiter>().AsSelf().SingleInstance();

        container = cb.Build();
    }

    #endregion

    #region Build GraphState

    private GraphState BuildGraph() {
        var s = new CustomSourceState(typeof(CustomSourceNode)) {
            Increment = increment,
            Count = count
        };
        var t = new CustomTransformState(typeof(CustomTransformNode)) {
            Constant = constant
        };
        var d = new CustomDrainState(typeof(CustomDrainNode));

        s.Output0.Connect(t.Input0);
        t.Output0.Connect(d.Input0);

        var g = new GraphState();
        g.Nodes.AddRange([s, t, d]);
        return g;
    }

    #endregion

    void IDisposable.Dispose() {
        container.Dispose();
    }

    #region Message, node states

    [DataContract]
    class IntMessage : GraphMessage {
        // Serialization
        static IntMessage() {
            KnownTypes.Add(typeof(IntMessage));
        }

        [DataMember]
        public int Data;
    }

    [DataContract]
    class CustomSourceState : SourceNodeState<IntMessage>
    {
        public CustomSourceState(Type implementingClass) : base(implementingClass) { }

        [DataMember]
        public int Increment;

        [DataMember]
        public int Count;
    }

    [DataContract]
    class CustomTransformState : TransformNodeState<IntMessage, IntMessage>
    {
        public CustomTransformState(Type implementingClass) : base(implementingClass) { }

        [DataMember]
        public int Constant;
    }

    // Drain state MUST accept GraphMessage
    class CustomDrainState : TransformNodeState<GraphMessage>
    {
        public CustomDrainState(Type implementingClass) : base(implementingClass) { }
        // Empty, but the base is abstract.
    }

    #endregion

    #region Node behavior

    class CustomSourceNode : SourceNode<CustomSourceState, IntMessage>
    {
        public CustomSourceNode(ILifetimeScope lifetimeScope, GraphShell owner, CustomSourceState state)
            : base(lifetimeScope, owner, state) { }

        protected override int ConcurrencyLimit => int.MaxValue;

        protected override async IAsyncEnumerable<IntMessage> GenerateAsync() {
            for (var i = 0; i < State.Count; ++i)
                yield return new() { Data = i * State.Increment };
        }
    }

    class CustomTransformNode : TransformNode<CustomTransformState, IntMessage, IntMessage>
    {
        public CustomTransformNode(ILifetimeScope lifetimeScope, GraphShell owner, CustomTransformState state)
            : base(lifetimeScope, owner, state) { }

        protected override int ConcurrencyLimit => int.MaxValue;

        // DO NOT modify the message in place.  In real-world workflows, the sam message can be enqueued at many input ports.
        protected override Task ProcessAsync(IntMessage message) {
            Console.WriteLine($"{GetType().Name}{PathId} RECEIVED: {message.Data}");
            Output0.Enqueue(new() { Data = message.Data - State.Constant });
            return Task.CompletedTask;
        }
    }

    class CustomDrainNode : DrainNode<CustomDrainState>
    {
        public CustomDrainNode(ILifetimeScope lifetimeScope, GraphShell owner, CustomDrainState state)
            : base(lifetimeScope, owner, state) { }

        protected override int ConcurrencyLimit => int.MaxValue;

        // Base implementation is a no-op.  Here we know that only a single message type exists in the graph.
        protected override Task ProcessAsync(GraphMessage message) {
            var typed = (IntMessage)message;
            Console.WriteLine($"{GetType().Name}{PathId} RECEIVED: {typed.Data}");
            return Task.CompletedTask;
        }
    }

    #endregion
}
