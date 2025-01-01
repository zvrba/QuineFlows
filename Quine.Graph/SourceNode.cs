using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Quine.HRCatalog;
using Quine.Schemas.Graph;

namespace Quine.Graph
{
    /// <summary>
    /// Source node has no inputs and generates messages on its only output.
    /// </summary>
    public abstract class SourceNode<TState, TMessage> : NodeShell<TState>
        where TState : SourceNodeState<TMessage>
        where TMessage : GraphMessage
    {
        public readonly OutputPort<TMessage> Output0;

        protected SourceNode(Autofac.ILifetimeScope lifetimeScope, GraphShell owner, TState state)
            : base(lifetimeScope, owner, state)
        {
            Output0 = new OutputPort<TMessage>(this, State.Output0);
        }

        protected override async Task MessageLoopAsync() {
            await foreach (var m in GenerateAsync())
                Output0.Enqueue(m);
        }

        /// <summary>
        /// Method called to generate the output messages.
        /// </summary>
        protected abstract IAsyncEnumerable<TMessage> GenerateAsync();
    }
}
