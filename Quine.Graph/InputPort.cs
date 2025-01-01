using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Quine.Schemas.Graph;
using Quine.HRCatalog;

namespace Quine.Graph;

public interface IInputPort : ITreeIdentity
{
    public event Action<IInputPort, GraphMessage> MessageEnqueued;
    internal IReadOnlyList<IOutputPort> Predecessors { get; }
    internal void Close(IOutputPort port);
    internal void Connect(IOutputPort port);
    internal void ClearEventSubscriptions();
}

interface IInputPort<in T> : IInputPort where T : GraphMessage
{
    void Enqueue(T message);
}

/// <summary>
/// Type-erased implementation of input port; <see cref="InputPort{T}"/> for a strongly-typed class.
/// </summary>
public sealed class InputPort<T> : GraphSchemaHook<InputPortState<T>>, IInputPort<T> where T : GraphMessage
{
    private readonly Channel<T> queue;
    private readonly List<IOutputPort> predecessors = new List<IOutputPort>();

    /// <summary>
    /// Constructs port from deserialized state.
    /// </summary>
    /// <param name="state">Serialized state.</param>
    /// <param name="owner">Node owning the port.</param>
    /// <exception cref="InvalidCastException">If state contains messages of type not compatible with <typeparamref name="T"/>.</exception>
    /// <remarks>
    /// Currently, all input port objects share the same signaling event from their owning node.
    /// </remarks>
    internal InputPort(NodeShell owner, InputPortState<T> state) : base(owner, state)
    {
#if false   // Does not play nice with changing assembly versions. Old workflow references old assembly version
        if (typeof(T).AssemblyQualifiedName != state.MessageType)
            throw new ArgumentException("Mismatch between runtime and serialized type.");
#endif
        queue = Channel.CreateUnbounded<T>(new UnboundedChannelOptions() {
            SingleReader = true,
            SingleWriter = false }
        );
    }

#region Explicit IInputPort<T> implementation

    IReadOnlyList<IOutputPort> IInputPort.Predecessors { get { return predecessors; } }

    void IInputPort.Close(IOutputPort port) {
        lock (predecessors) {
            QHEnsure.State(predecessors.Remove(port));
            if (predecessors.Count == 0)
                queue.Writer.Complete();
        }
    }

    void IInputPort.Connect(IOutputPort port) {
        QHEnsure.State(!predecessors.Contains(port));
        predecessors.Add(port);
    }

    void IInputPort<T>.Enqueue(T m) {
        if (m == null)
            throw new ArgumentNullException(nameof(m));
        try { MessageEnqueued?.Invoke(this, m); }
        catch { }
        QHEnsure.State(queue.Writer.TryWrite(m));
    }

#endregion

    /// <summary>
    /// Dequeues a message. Blocks  until a message is available.
    /// </summary>
    /// <returns>
    /// The dequeued message.  In the current implementation, this method never returns null. In future
    /// version, when support for multiple inputs per node is added, this may be allowed to return null.
    /// </returns>
    /// <exception cref="ChannelClosedException">All output ports connected to this port have been closed.</exception>
    public async Task<T> Dequeue(CancellationToken token) {
        bool moreData = await queue.Reader.WaitToReadAsync(token);
        if (!moreData) {
            throw new ChannelClosedException();
        }
        return await queue.Reader.ReadAsync(token);
    }

    /// <summary>
    /// Called before the message has been enqueued to the port.  The arguments to the event are the
    /// sending port and the message being enqueued.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The main purpose of this event is signaling progress / adding to the message's processing
    /// history.  Therefore the event is called before enqueueing the message to avoid the race
    /// condition where the dequeueing thread could append "Accepted" to the hisory before the
    /// enqueueing thread has managed to append "Queued" to the history.
    /// </para>
    /// </remarks>
    public event Action<IInputPort, GraphMessage> MessageEnqueued;

    void IInputPort.ClearEventSubscriptions() => MessageEnqueued = null;
}
