using System;
using System.Diagnostics;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Quine.FileTransfer;

internal sealed class ConsumerStateMachine : TransferStateMachine
{
    private Channel<AlignedBuffer> channel = null!;

    internal ConsumerStateMachine(TransferDriver driver, ITransferConsumer worker) : base(driver, worker) { }

    internal void EnqueueItem(AlignedBuffer? buffer) {
        var b = true;
        if (buffer is null) channel.Writer.Complete();
        else b = channel.Writer.TryWrite(buffer);
        Trace.Assert(b, "Writing to channel failed.");
    }

    private protected override async Task SpawnTasks() {
        this.channel = Channel.CreateUnbounded<AlignedBuffer>(new() {
            AllowSynchronousContinuations = true,
            SingleReader = Worker.MaxConcurrency == 1,
            SingleWriter = true,    // This is true also for parallel producer (always enqueued by single task)
        });
        try {
            await base.SpawnTasks();
        }
        finally {
            // Keep reading and returning buffers until the channel is closed and emptied.
            try {
                await foreach (var buffer in channel.Reader.ReadAllAsync())
                    Driver.Return(buffer);
            }
            catch (ChannelClosedException) {
                // NOOP
            }
        }
    }

    private protected override async Task SingleTaskWork() {
        AlignedBuffer? buffer = null;
        try {
            while (true) {
                Trace.Assert(buffer is null, "Buffer not released before consumer iteration.");
                buffer = await channel.Reader.ReadAsync(CancellationToken);

                Trace.Assert(buffer.Memory.Length == BlockSize, "Buffer size does not match that pool's block size.");
                Trace.Assert(buffer.Data.Length > 0, "EOF must be signalled by closing the channel.");
                
                await ((ITransferConsumer)Worker).DrainAsync(buffer);
                Driver.Return(buffer);
                buffer = null;
            }
        }
        catch (ChannelClosedException) {
            // Swallow: we're done due to EOF.
        }
        catch (Exception e) {
            RecordExceptionAndCancelSelf(e);
        }
        finally {
            if (buffer is not null)
                Driver.Return(buffer);
        }
    }
}
