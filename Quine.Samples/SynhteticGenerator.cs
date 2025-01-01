using Quine.FileTransfer;

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Quine.Samples;

internal abstract class SyntheticGenerator : ITransferWorker
{
    private readonly AlignedBuffer memory;
    private int bytesTransferred;

    private SyntheticGenerator() {
        memory = new(null!, FixtureConfiguration.MaxDataSize);
    }

    public SyntheticParameters Parameters { get; set; } = default!;

    public TransferStateMachine State { get; set; } = default!;

    public int MaxConcurrency => Parameters.MaxConcurrency;

    public int BytesTransferred => bytesTransferred;

    public Task InitializeAsync() {
        bytesTransferred = 0;
        memory.GetSpan().Fill(0xFF);
        Parameters.InjectIOException(SyntheticParameters.InitializeBlock);
        return Task.CompletedTask;
    }

    public Task<byte[]?> FinalizeAsync(ITransferHasher? transferHasher, ITransferBuffer buffer) {
        byte[]? hash = null;

        Parameters.InjectIOException(SyntheticParameters.FinalizeBlock);

        if (transferHasher is not null) {
            transferHasher.Append(memory.GetSpan()[..bytesTransferred]);
            hash = transferHasher.GetHashAndReset();
        }

        return Task.FromResult(hash);
    }

    internal sealed class Producer : SyntheticGenerator, ITransferProducer {
        public async Task<int> FillAsync(ITransferBuffer buffer) {
            var ret = Math.Min(Parameters.DataLength - buffer.Sequence * State.BlockSize, buffer.Memory.Length);
            if (ret > 0) {
                buffer.Memory.Span[..ret].Fill((byte)buffer.Sequence);
                Interlocked.Add(ref bytesTransferred, ret);

                try {
                    var block = memory.Memory.Slice(buffer.Sequence * State.BlockSize);
                    buffer.Memory[..ret].CopyTo(block);

                    // Give out uncorrupted block, corrupt the private copy.
                    if (Parameters.SimulateCorruption == buffer.Sequence)
                        ++block.Span[0];

                }
                catch (ArgumentOutOfRangeException) when (ret == 0) {
                    // Computation overflow, ignore.
                }
            } else {
                ret = 0;
            }

            await Parameters.DelayAsync(ret);
            Parameters.InjectIOException(buffer.Sequence);
            return ret;
        }
    }

    internal sealed class Consumer : SyntheticGenerator, ITransferConsumer {
        public async Task DrainAsync(ITransferBuffer buffer) {
            Interlocked.Add(ref bytesTransferred, buffer.Data.Length);
            var offset = buffer.Sequence * State.BlockSize;
            var block = memory.Memory[offset..];

            buffer.Data.CopyTo(block);
            await Parameters.DelayAsync(buffer.Data.Length);
            Parameters.InjectIOException(buffer.Sequence);

            // Corrupt private copy.
            if (Parameters.SimulateCorruption == buffer.Sequence)
                ++block.Span[0];
        }
    }
}
