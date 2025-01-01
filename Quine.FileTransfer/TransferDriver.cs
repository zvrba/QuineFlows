using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quine.FileTransfer;

/// <summary>
/// This class is the main entry point for starting transfers.  The same instance should be reused for many executions,
/// though not concurrently.  This class allocates native memory (the amount depends on the parameters passed to ctor)
/// which is held until disposal.
/// </summary>
public sealed class TransferDriver : IDisposable
{
    /// <summary>
    /// Constructor.  Initializes properties that are fixed across individual executions.
    /// </summary>
    /// <param name="bufferSize">Size of individual buffer.</param>
    /// <param name="capacity">Number of buffers to allocate.</param>
    public TransferDriver(int bufferSize, int capacity)
    {
        BufferPool = new(bufferSize, capacity);
    }

    private bool _isDisposed;

    /// <inheritdoc/>
    public void Dispose() {
        if (_isDisposed)
            return;
        BufferPool.Dispose();
        _isDisposed = true;
    }

    internal TransferBufferPool BufferPool { get; }
    
    /// <summary>
    /// Producer side of the transfer.
    /// </summary>
    public ITransferProducer Producer { get; set; } = null!;

    /// <summary>
    /// Consumer sides of the transfer.
    /// </summary>
    public IReadOnlyList<ITransferConsumer> Consumers { get; set; } = null!;

    /// <summary>
    /// If this delegate is provided, a reference hash will be computed while reading the file.  The hash can be obtained
    /// through <see cref="ReferenceHash"/> property after completed execution.  In addition, when <see cref="VerifyHash"/>, each
    /// worker will perform a 2nd-pass hash verification.
    /// </summary>
    public Func<ITransferHasher>? HasherFactory { get; set; }

    /// <summary>
    /// If true and <see cref="HasherFactory"/> has been provided, the file's hash will be verified after a successfully completed transfer.
    /// </summary>
    public bool VerifyHash { get; set; }

    /// <summary>
    /// Hash value computed during file reading.
    /// </summary>
    public byte[]? ReferenceHash => ReferenceHasher?.Hash;

    internal FileHasher? ReferenceHasher { get; private set; }

    /// <summary>
    /// Executes the transfer as defined by the public properties.
    /// </summary>
    /// <param name="ct">Cancellation token that may be used to cancel the transfer.</param>
    /// <exception cref="ArgumentException">
    /// Thrown in the following cases
    /// <list type="bullet">
    /// <item>Producer is not provided, or at least one consumer is not provided.</item>
    /// <item>Hash verification is requested, but the buffer pool capacity is less than the number of consumers plus one.</item>
    /// <item>Hash verification is requested, but <see cref="HasherFactory"/> is not provided.</item>
    /// </list>
    /// </exception>
    /// <returns>Task.</returns>
    public async Task ExecuteAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (Producer is null || Consumers is not { Count: > 0 })
            throw new ArgumentException("Invalid Producer/Consumers configuration.");
        if (BufferPool.Capacity < 1 + Consumers.Count)
            throw new ArgumentException("Insufficient buffer pool capacity for hash verification.");
        if (VerifyHash && HasherFactory is null)
            throw new ArgumentException($"{nameof(HasherFactory)} must be provided for hash verification.");


        BufferPool.Invariant();

        try {
            ReferenceHasher = HasherFactory is null ? null : new(HasherFactory());
            GlobalCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);

            // Create states.
            Producer.State = new ProducerStateMachine(this, Producer);
            foreach (var c in Consumers)
                c.State = new ConsumerStateMachine(this, c);
            if (ReferenceHasher is not null)
                ReferenceHasher.State = new ConsumerStateMachine(this, ReferenceHasher);

            // Consumers must start up before producer.
            var tasks = new Task[1 + Consumers.Count + (ReferenceHasher is null ? 0 : 1)];
            for (var i = 0; i < Consumers.Count; ++i)
                tasks[i + 1] = Consumers[i].State.Completion = Consumers[i].State.RunAsync();
            if (ReferenceHasher is not null)
                tasks[^1] = ReferenceHasher.State.Completion = ReferenceHasher.State.RunAsync();
            tasks[0] = Producer.State.Completion = Producer.State.RunAsync();
            
            await Task.WhenAll(tasks);
        }
        finally {
            GlobalCancellation.Dispose();
            GlobalCancellation = null!;

            ReferenceHasher?.Dispose();
            ReferenceHasher = null;
        }
        
        BufferPool.Invariant();
    }

    /// <summary>
    /// Cancels an ongoing transfer independently from the token passed to <see cref="ExecuteAsync(CancellationToken)"/>.
    /// This is a no-op if the transfer has already finished.
    /// </summary>
    public void Cancel() => GlobalCancellation?.Cancel();

    #region Internal, for use by the state machines

    // Signalled under three conditions:
    // - Externally provided CT (to ExecuteAsync) is cancelled
    // - Call to Cancel()
    // - Call to Fail() when it's pointless to keep running
    internal CancellationTokenSource GlobalCancellation { get; private set; } = null!;

    internal Task<AlignedBuffer> RentAsync(CancellationToken ct) => BufferPool.RentAsync(ct);

    internal void Return(AlignedBuffer item) => BufferPool.Return(item);

    internal void Broadcast(AlignedBuffer? payload) {
        if (payload is not null) {
            // NB: The producer doesn't explicitly return the buffer.  We just compute the correct reference count.
            Trace.Assert(payload._UseCount == 1, "Must be owned only by producer.");
            payload._UseCount += Consumers.Count - (ReferenceHasher is null ? 1 : 0);
        }

        ((ConsumerStateMachine?)ReferenceHasher?.State)?.EnqueueItem(payload);
        for (int i = 0; i < Consumers.Count; ++i)
            ((ConsumerStateMachine)Consumers[i].State).EnqueueItem(payload);
    }

    internal void Fail(TransferStateMachine sm) {
        if (sm is ProducerStateMachine || sm.Worker is FileHasher || Consumers.All(x => x.State.IsFaulted))
            GlobalCancellation.Cancel();
    }

    #endregion
}
