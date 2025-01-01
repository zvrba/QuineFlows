using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Quine.HRCatalog;

namespace Quine.Schemas.Graph
{
    /// <summary>
    /// Helper interface for creating connections.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    public interface IConnectionTarget<in T> where T : GraphMessage
    { }

    /// <summary>
    /// Helper interface for creating connections.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    public interface IConnectionSource<out T> where T : GraphMessage
    {
        /// <summary>
        /// Connects <c>this</c> (a port) to target.
        /// </summary>
        /// <param name="target">Target port to connect to.</param>
        void Connect(IConnectionTarget<T> target);
    }

    /// <summary>
    /// Base class for input and output ports.
    /// </summary>
    [DataContract(Namespace = XmlNamespaces.Graph, IsReference = true)]
    [KnownType("GetKnownTypes")]
    public abstract class PortStateBase : GraphRuntimeHook
    {
        /// <summary>
        /// All concrete port types must be registered here; <see cref="RegisterMessageType(Type)"/>.
        /// </summary>
        protected static readonly HashSet<Type> KnownTypes = new HashSet<Type>();

        /// <summary>
        /// Assembly-qualified name of the port's message type.
        /// </summary>
        [DataMember]
        public string MessageType { get; set; }

        static PortStateBase() {
            RegisterMessageType(typeof(GraphMessage));
        }

        /// <summary>
        /// This method is used to register message types that are carried over ports.
        /// </summary>
        /// <param name="type">Message type to register.</param>
        public static void RegisterMessageType(Type type) {
            QHEnsure.NotNull(type);
            QHEnsure.Value(type, typeof(GraphMessage).IsAssignableFrom(type));
            KnownTypes.Add(typeof(InputPortState<>).MakeGenericType(type));
            KnownTypes.Add(typeof(OutputPortState<>).MakeGenericType(type));
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="messageType">Type of message accepted by the port.</param>
        protected PortStateBase(Type messageType) {
            this.MessageType = messageType.AssemblyQualifiedName;
        }

        private static IEnumerable<Type> GetKnownTypes() {
            return KnownTypes;
        }
    }

    /// <summary>
    /// Input port stores incoming messages.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    [DataContract(Namespace = XmlNamespaces.Graph, IsReference = true)]
    public sealed class InputPortState<T> : PortStateBase, IConnectionTarget<T> where T : GraphMessage
    {
        static InputPortState() {
            KnownTypes.Add(typeof(InputPortState<T>));
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public InputPortState() : base(typeof(T)) { }
    }

    /// <summary>
    /// Output port is used to send outgoing messages.
    /// </summary>
    /// <typeparam name="T">Message type.</typeparam>
    [DataContract(Namespace = XmlNamespaces.Graph, IsReference = true)]
    public sealed class OutputPortState<T> : PortStateBase, IConnectionSource<T> where T : GraphMessage
    {
        static OutputPortState() {
            KnownTypes.Add(typeof(OutputPortState<T>));
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public OutputPortState() : base(typeof(T)) { }

        /// <summary>
        /// Input ports that will receive messages sent to this output port.
        /// </summary>
        [DataMember]
        public List<PortStateBase> Successors { get; set; } = new List<PortStateBase>();

        /// <summary>
        /// Connect <c>this</c> to target.
        /// </summary>
        /// <param name="target">Target to connect to.</param>
        public void Connect(IConnectionTarget<T> target) {
            QHEnsure.State(!Successors.Contains((PortStateBase)target));
            Successors.Add((PortStateBase)target);
        }
    }
}
