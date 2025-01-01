using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Quine.HRCatalog;

namespace Quine.Schemas.Graph
{
    /// <summary>
    /// Class for serializing the node state.  The state of any concrete node must inherit from this class.
    /// </summary>
    [DataContract(Namespace = XmlNamespaces.Graph)]
    [KnownType("GetKnownTypes")]
    public abstract class NodeStateBase : GraphRuntimeHook
    {
        private static IEnumerable<Type> GetKnownTypes() {
            return KnownTypes;
        }

        /// <summary>
        /// Derived types must be added to this collection.
        /// </summary>
        protected static readonly HashSet<Type> KnownTypes = new HashSet<Type>();

        /// <summary>
        /// Assembly-qualified name of the implementing type.
        /// </summary>
        [DataMember]
        public string ImplementingType { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="implementingClass">
        /// The type tha implements the run-time behaviour.  Instantiated dynamically at run-time.
        /// </param>
        protected NodeStateBase(Type implementingClass) {
            this.ImplementingType = QHEnsure.NotEmpty(implementingClass.AssemblyQualifiedName);
        }

        /// <summary>
        /// Assigns values to <see cref="InputPorts"/> and <see cref="OutputPorts"/>.
        /// </summary>
        /// <param name="inputPorts">Array of input ports.</param>
        /// <param name="outputPorts">Array of output ports.</param>
        /// <seealso cref="SetPorts()"/>
        protected void SetPorts(PortStateBase[] inputPorts, PortStateBase[] outputPorts) {
            InputPorts = inputPorts;
            OutputPorts = outputPorts;
        }

        /// <summary>
        /// This method is a deserialization hook.  Subclasses must implement this so that <see cref="InputPorts" />
        /// and <see cref="OutputPorts" /> are set correctly also after deserialization.
        /// </summary>
        /// <seealso cref="SetPorts()"/>
        protected abstract void SetPorts();

        [OnDeserialized]
        private void SetPorts(StreamingContext ctx) {
            SetPorts();
        }

        /// <summary>
        /// Array of input ports.
        /// </summary>
        public PortStateBase[] InputPorts { get; private set; }

        /// <summary>
        /// Array of output ports.
        /// </summary>
        public PortStateBase[] OutputPorts { get; private set; }

        /// <summary>
        /// Trace events generated during the execution of this node.
        /// </summary>
        [DataMember]
        public Schemas.Core.Eventing.OperationalTrace Trace { get; internal protected set; }

        /// <summary>
        /// The node's completion state.
        /// </summary>
        [DataMember]
        public GraphRunState CompletionState { get; set; }

        /// <summary>
        /// The override ensures that ports get assigned sequential ids.
        /// </summary>
        public override void SetId(ITreeIdentity owner, int id) {
            base.SetId(owner, id);
            
            int i;
            for (i = 0; i < InputPorts.Length; ++i)
                InputPorts[i].SetId(this, i);
            for (; i < OutputPorts.Length; ++i)
                OutputPorts[i].SetId(this, i);
        }
    }

    /// <summary>
    /// Source node generates messages; it has no inputs.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    [DataContract(Namespace = XmlNamespaces.Graph)]
    public abstract class SourceNodeState<T> : NodeStateBase where T : GraphMessage
    {
        static SourceNodeState() {
            KnownTypes.Add(typeof(SourceNodeState<T>));
        }

        /// <summary>
        /// The node's output.
        /// </summary>
        [DataMember]
        public readonly OutputPortState<T> Output0 = new OutputPortState<T>();

        /// <inheritdoc/>
        protected SourceNodeState(Type implementingClass) : base(implementingClass) {
            SetPorts();
        }

        /// <inheritdoc/>
        protected sealed override void SetPorts() {
            SetPorts(Array.Empty<PortStateBase>(), new PortStateBase[] { Output0 });
        }
    }

    /// <summary>
    /// This is a "drain" node: it accepts messages without producing any output.
    /// </summary>
    /// <typeparam name="TInput">Message type.</typeparam>
    [DataContract(Namespace = XmlNamespaces.Graph)]
    public abstract class TransformNodeState<TInput> : NodeStateBase where TInput : GraphMessage
    {
        static TransformNodeState() {
            KnownTypes.Add(typeof(TransformNodeState<TInput>));
        }

        /// <summary>
        /// The node's input.
        /// </summary>
        [DataMember]
        public readonly InputPortState<TInput> Input0 = new InputPortState<TInput>();

        /// <inheritdoc/>
        protected TransformNodeState(Type implementingClass) : base(implementingClass) {
            SetPorts();
        }

        /// <inheritdoc/>
        protected override void SetPorts() {
            SetPorts(new PortStateBase[] { Input0 }, Array.Empty<PortStateBase>());
        }
    }

    /// <summary>
    /// This node acts as a simple function, mapping single input to single output.
    /// </summary>
    /// <typeparam name="TInput">Type of input message.</typeparam>
    /// <typeparam name="TOutput0">Type of output message.</typeparam>
    [DataContract(Namespace = XmlNamespaces.Graph)]
    public abstract class TransformNodeState<TInput, TOutput0> : TransformNodeState<TInput>
        where TInput : GraphMessage
        where TOutput0 : GraphMessage
    {
        static TransformNodeState() {
            KnownTypes.Add(typeof(TransformNodeState<TInput, TOutput0>));
        }

        /// <summary>
        /// The node's output.
        /// </summary>
        [DataMember]
        public readonly OutputPortState<TOutput0> Output0 = new OutputPortState<TOutput0>();

        /// <inheritdoc/>
        protected TransformNodeState(Type implementingClass) : base(implementingClass) {
            SetPorts();
        }

        /// <inheritdoc/>
        protected override void SetPorts() {
            SetPorts(new PortStateBase[] { Input0 }, new PortStateBase[] { Output0 });
        }
    }

    /// <summary>
    /// This node maps single input type to two possible output types.
    /// </summary>
    /// <typeparam name="TInput">Type of input message.</typeparam>
    /// <typeparam name="TOutput0">Type of output message on the 1st output port.</typeparam>
    /// <typeparam name="TOutput1">Type of output message on the 2nd output port.</typeparam>
    [DataContract(Namespace = XmlNamespaces.Graph)]
    public abstract class TransformNodeState<TInput, TOutput0, TOutput1> : TransformNodeState<TInput, TOutput0>
        where TInput : GraphMessage
        where TOutput0 : GraphMessage
        where TOutput1 : GraphMessage
    {
        static TransformNodeState() {
            KnownTypes.Add(typeof(TransformNodeState<TInput, TOutput0, TOutput1>));
        }

        /// <summary>
        /// The node's 2nd output.
        /// </summary>
        [DataMember]
        public readonly OutputPortState<TOutput1> Output1 = new OutputPortState<TOutput1>();

        /// <inheritdoc/>
        protected TransformNodeState(Type implementingClass) : base(implementingClass) {
            SetPorts();
        }

        /// <inheritdoc/>
        protected override void SetPorts() {
            SetPorts(new PortStateBase[] { Input0 }, new PortStateBase[] { Output0, Output1 });
        }
    }
}
