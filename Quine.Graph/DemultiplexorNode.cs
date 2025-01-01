using System;
using System.Linq;
using System.Threading.Tasks;

using Quine.HRCatalog;
using Quine.Schemas.Graph;

namespace Quine.Graph
{
    public class DemultiplexorNode<TPayload> :
        TransformNode<DemultiplexorNodeState<TPayload>, MultiplexedMessage<TPayload>>
        where TPayload : GraphMessage
    {
        public readonly OutputPort<TPayload>[] Output;

        public DemultiplexorNode(Autofac.ILifetimeScope lifetimeScope, GraphShell owner, DemultiplexorNodeState<TPayload> state)
            : base(lifetimeScope, owner, state)
        {
            Output = Enumerable.Range(0, state.Output.Length)
                .Select(i => new OutputPort<TPayload>(this, state.Output[i]))
                .ToArray();
        }

        protected internal override int ConcurrencyLimit => int.MaxValue;

        protected override Task ProcessAsync(MultiplexedMessage<TPayload> message) {
            QHEnsure.Value(message.Tag, message.Tag >= 0 || message.Tag < Output.Length);
            Output[message.Tag].Enqueue(message.Payload);
            return Task.CompletedTask;
        }
    }
}
