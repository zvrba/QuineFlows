using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quine.FileTransfer;

/// <summary>
/// Manages equally-sized preallocated blocks on unmanaged heap; <see cref="AlignedBuffer"/>.
/// Blocks are aligned at boundary of 4096 bytes.
/// </summary>
internal sealed class TransferBufferPool : IDisposable {

    readonly SemaphoreSlim availableItemCount;                  // # of free buffers in availableBuffers.
    readonly Queue<AlignedBuffer> availableItems;              // Storage; index 0 is reserved for the head.

    /// <summary>
    /// Constructor.  Preallocates a single large pinned block from the unmanaged heap and carves it into blocks.
    /// </summary>
    /// <param name="blockSize">Size of single buffer.  Must be a multiple of <see cref="ITransferBuffer.SectorSize"/>.</param>
    /// <param name="capacity">Total number of buffers to preallocate.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="blockSize"/> is not a positive multiple of <see cref="ITransferBuffer.SectorSize"/>.
    /// </exception>
    public TransferBufferPool(int blockSize, int capacity) {
        if (blockSize < ITransferBuffer.SectorSize || blockSize % ITransferBuffer.SectorSize != 0)
            throw new ArgumentOutOfRangeException(nameof(blockSize), $"Block size must be a multiple of page size (${ITransferBuffer.SectorSize} bytes).");

        this.BlockSize = blockSize;
        this.Capacity = capacity;
        this.availableItemCount = new SemaphoreSlim(Capacity, Capacity);
        this.availableItems = new Queue<AlignedBuffer>(Capacity + 4);
        for (var i = 0; i < capacity; ++i)
            availableItems.Enqueue(new(this, blockSize));
    }

    public void Dispose() {
        if (IsDisposed)
            return;
        try {
            while (availableItems.Count > 0)
                ((IDisposable)availableItems.Dequeue()).Dispose();
            availableItemCount.Dispose();
        }
        finally {
            IsDisposed = true;
        }
    }

    /// <summary>
    /// Size of individual buffers handed out by the buffer. Initialized by ctor.
    /// </summary>
    public int BlockSize { get; }

    /// <summary>
    /// Number of preallocated buffers.  Initialized by ctor.
    /// </summary>
    public int Capacity { get; }

    /// <summary>
    /// True if this instance has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Must be true before and after execution.
    /// </summary>
    internal void Invariant() {
        if (availableItemCount.CurrentCount != Capacity || availableItems.Any(x => x._UseCount != 0))
            throw new NotImplementedException("BUG: TransferBufferPool invariant violated.");
    }

    /// <summary>
    /// Waits for a buffer to be available and rents it from the pool.
    /// </summary>
    /// <returns>An available buffer with use count of 1.</returns>
    public async Task<AlignedBuffer> RentAsync(CancellationToken ct) {
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        AlignedBuffer bufferItem;
        await availableItemCount.WaitAsync(ct);
        lock (availableItems)
            bufferItem = availableItems.Dequeue();

        Trace.Assert(bufferItem._Owner == this);
        Trace.Assert(bufferItem._UseCount == 0);

        bufferItem._UseCount = 1;
        return bufferItem;
    }

    /// <summary>
    /// Decreases the item's reference count and returns it to the pool when it reaches 0.
    /// </summary>
    /// <param name="bufferItem">Item to return.  Must belong to this buffer.</param>
    /// <returns>
    /// True if the buffer's reference count reached 0.
    /// </returns>
    public bool Return(AlignedBuffer bufferItem) {
        Trace.Assert(bufferItem._Owner == this);
        ObjectDisposedException.ThrowIf(IsDisposed, this);

        if (Interlocked.Decrement(ref bufferItem._UseCount) > 0)
            return false;
        Trace.Assert(bufferItem._UseCount == 0);    // As opposed to negative.

        lock (availableItems)
            availableItems.Enqueue(bufferItem);
        availableItemCount.Release();
        return true;
    }
}
