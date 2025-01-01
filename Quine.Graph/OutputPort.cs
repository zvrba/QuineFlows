using System;
using System.Collections.Generic;
using System.Linq;

using Quine.HRCatalog;
using Quine.Schemas.Graph;

namespace Quine.Graph
{
    interface IOutputPort : ITreeIdentity
    {
        IEnumerable<IInputPort> Successors { get; }
        void Close();
        void Connect();
    }

    interface IOutputPort<T> : IOutputPort where T : GraphMessage
    {
        new IReadOnlyList<IInputPort<T>> Successors { get; }
    }

    /// <summary>
    /// Type-erased implementation of output port.
    /// </summary>
    public sealed class OutputPort<T> : GraphSchemaHook<OutputPortState<T>>, IOutputPort<T> where T : GraphMessage
    {
        private readonly List<IInputPort<T>> successors = new List<IInputPort<T>>();
        private volatile bool closed;

        /// <summary>
        /// Constructs port from deserialized state. <see cref="InputPort{T}"/>.
        /// </summary>
        internal OutputPort(NodeShell owner, OutputPortState<T> state) : base(owner, state)
        {
#if false   // Does not play nice with changing assembly versions. Old workflow references old assembly version
            if (typeof(T).AssemblyQualifiedName != state.MessageType)
                throw new ArgumentException("Mismatch between runtime and serialized type.");
            closed = State.Closed;
#endif
            // successors lazily initialized in Connect() after all nodes and input ports have been created.
        }

#region Explicit IOutputPort implementation

        IEnumerable<IInputPort> IOutputPort.Successors => successors;
        IReadOnlyList<IInputPort<T>> IOutputPort<T>.Successors => successors;

        void IOutputPort.Connect() {
            QHEnsure.State(successors.Count == 0);
            successors.AddRange(State.Successors.Select(s => (IInputPort<T>)s.RuntimeObject));

            QHEnsure.State(successors.Count > 0);
            foreach (var s in successors)
                s.Connect(this);

            if (closed)
                Close();
        }

#endregion

        /// <summary>
        /// Enqueues a message to all connected input ports.
        /// </summary>
        public void Enqueue(T m) {
            if (m == null)
                throw new ArgumentNullException(nameof(m));
            QHEnsure.State(m.Id != Guid.Empty);
            QHEnsure.State(!closed);
            foreach (var s in successors)
                s.Enqueue(m);
        }

        public void Close() {
            QHEnsure.State(!closed);
            closed = true;
            foreach (var s in successors)
                s.Close(this);
            //State.Closed = true;
        }
    }
}
