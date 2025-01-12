using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Quine.FileTransfer;

internal sealed class ProducerStateMachine : TransferStateMachine
{
    private readonly PriorityQueue<AlignedBuffer, int> drainqueue;
    
    internal ProducerStateMachine(TransferDriver driver, ITransferProducer worker) : base(driver, worker)
    {
        this.drainqueue = new(Driver.BufferPool.Capacity);
    }

    private int fillsequence;
    private int drainsequence;
    private int lastfilllength;

    private protected override async Task SpawnTasks() {
        fillsequence = 0;
        drainsequence = 0;
        lastfilllength = BlockSize;

        try {
            await base.SpawnTasks();
        }
        finally {
            // In case of parallel producer: empty the queue when one task encountered exception.
            while (drainqueue.TryDequeue(out var buffer, out var _))
                Driver.Return(buffer);
            Driver.Broadcast(null);
        }
    }

    private protected override async Task SingleTaskWork() {
        AlignedBuffer? buffer = null;
        try {
            while (true) {
                Trace.Assert(buffer is null);
                buffer = await Driver.RentAsync(CancellationToken);

                Trace.Assert(buffer.Memory.Length == BlockSize, "Buffer size does not match that pool's block size.");
                Trace.Assert(buffer._UseCount == 1, $"Buffer's use count was {buffer._UseCount} instead of 1.");

                buffer._Sequence = Interlocked.Increment(ref fillsequence) - 1;  // Value BEFORE increment.
                var len = await ((ITransferProducer)Worker).FillAsync(buffer);
                if (len == 0)
                    break;
                buffer._Data = buffer.Memory[..len];

                lock (drainqueue) {
                    drainqueue.Enqueue(buffer, buffer.Sequence);
                    while (drainqueue.TryPeek(out buffer, out var seq) && seq == drainsequence) {
                        buffer = drainqueue.Dequeue();
                        Trace.Assert(buffer.Sequence == drainsequence);

                        Trace.Assert(buffer.Data.Length <= lastfilllength);
                        lastfilllength = buffer.Data.Length;

                        // WARNING: This must come after Dequeue() otherwise the buffer would be returned twice:
                        // once by finally as it's set by TryPeek, once by RunAsyncTask as it's still in the queue.
                        CancellationToken.ThrowIfCancellationRequested();
                        Driver.Broadcast(buffer);
                        ++drainsequence;
                    }
                    buffer = null;
                }
            }
        }
        catch (Exception e) {
            RecordExceptionAndCancelSelf(e);
        }
        finally {
            // Counts towards progress only if handed out to consumers.
            if (buffer is not null)
                Driver.Return(buffer);
        }
    }

    // TODO: Own task to dequeue and broadcast.
}
