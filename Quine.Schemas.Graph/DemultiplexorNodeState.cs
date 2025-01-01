using System;
using System.Linq;
using System.Runtime.Serialization;

namespace Quine.Schemas.Graph
{
    /// <summary>
    /// Demultiplexes a message, tagged with an integer, to one of the predetermined number of output ports.
    /// </summary>
    /// <typeparam name="TPayload"></typeparam>
    [DataContract(Namespace = XmlNamespaces.Graph)]
    public class DemultiplexorNodeState<TPayload> : TransformNodeState<MultiplexedMessage<TPayload>> where TPayload: GraphMessage
    {
        static DemultiplexorNodeState() {
            KnownTypes.Add(typeof(DemultiplexorNodeState<TPayload>));
        }

        /// <summary>
        /// Output ports of this node.
        /// </summary>
        [DataMember]
        public readonly OutputPortState<TPayload>[] Output;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="implementingClass">Implementing class.</param>
        /// <param name="outputCount">Number of outputs to demultiplex to.</param>
        public DemultiplexorNodeState(Type implementingClass, int outputCount) : base(implementingClass) {
            Output = Enumerable.Range(0, outputCount).Select(x => new OutputPortState<TPayload>()).ToArray();
            SetPorts();
        }

        /// <inheritdoc/>
        protected override void SetPorts() {
            SetPorts(new PortStateBase[] { Input0 }, Output);
        }
    }

    /// <summary>
    /// Message type handled by <see cref="DemultiplexorNodeState{TPayload}"/>.
    /// </summary>
    /// <typeparam name="T">The type of payload being demultiplexed.</typeparam>
    [DataContract(Namespace = XmlNamespaces.Graph)]
    public class MultiplexedMessage<T> : GraphMessage where T: GraphMessage
    {
        static MultiplexedMessage() {
            KnownTypes.Add(typeof(MultiplexedMessage<T>));
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="tag">Index of <see cref="DemultiplexorNodeState{TPayload}.Output"/> port to which the message should be sent.</param>
        /// <param name="payload">The actual payload.</param>
        public MultiplexedMessage(int tag, T payload) {
            this.tag = tag;
            this.payload = payload;
        }

        /// <summary>
        /// Message tag (index of the destination output port).
        /// </summary>
        public int Tag => tag;
        [DataMember(Name = "Tag")]
        private readonly int tag;

        /// <summary>
        /// Actual payload.
        /// </summary>
        public T Payload => payload;
        [DataMember(Name = "Payload")]
        private readonly T payload;
    }
}
