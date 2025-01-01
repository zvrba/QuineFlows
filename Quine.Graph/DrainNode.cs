using System;
using System.Threading.Tasks;

using Quine.Schemas.Graph;

namespace Quine.Graph
{
    /// <summary>
    /// Terminal node for a graph.  Needed because all output ports must be connected to at least one input port.
    /// </summary>
    public abstract class DrainNode<TState> : TransformNode<TState, GraphMessage>
        where TState : TransformNodeState<GraphMessage>
    {
        public DrainNode(Autofac.ILifetimeScope lifetimeScope, GraphShell owner, TState state)
            : base(lifetimeScope, owner, state) { }

        protected override Task ProcessAsync(GraphMessage message) => Task.CompletedTask;
    }
}
