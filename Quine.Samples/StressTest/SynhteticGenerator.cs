using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Quine.FileTransfer;

namespace Quine.Samples.StressTest;

internal abstract class SyntheticGenerator : ITransferWorker
{
    private readonly AlignedBuffer stream;
    private int bytesTransferred;

    private SyntheticGenerator()
    {
        stream = new(null!, FixtureConfiguration.MaxDataSize);
    }

    public SyntheticParameters Parameters { get; set; } = default!;

    public TransferStateMachine State { get; set; } = default!;

    public int MaxConcurrency => Parameters.MaxConcurrency;

    public int BytesTransferred => bytesTransferred;

    public virtual Task InitializeAsync()
    {
        bytesTransferred = 0;
        if (Parameters.SimulateIOError == 'I')
            InjectIOException("InitializeAsync");
        return Task.CompletedTask;
    }

    public Task<byte[]?> FinalizeAsync(ITransferHasher? transferHasher, ITransferBuffer buffer)
    {
        byte[]? hash = null;

        if (Parameters.SimulateIOError == 'F')
            InjectIOException("FinalizeAsync");

        if (transferHasher is not null)
        {
            transferHasher.Append(stream.GetSpan()[..bytesTransferred]);
            hash = transferHasher.GetHashAndReset();
        }

        return Task.FromResult(hash);
    }

    static void InjectIOException(string where)
        => throw new IOException($"Simulated IO error in {where}.");

    async Task DelayAsync(int byteCount)
    {
        if (!Parameters.SimulateLatency.HasValue)
            return;

        // Both are in milliseconds.
        var delay = Random.Shared.Next(Parameters.SimulateLatency.Value.Low, Parameters.SimulateLatency.Value.High);
        if (delay > 0)
            await Task.Delay(delay);
    }


    internal sealed class Producer : SyntheticGenerator, ITransferProducer
    {
        // Producer is reused and we never corrupt the reference stream, so initialize it only once.
        public Producer() {
            var s = stream.GetSpan();
            for (var i = 0; i < s.Length; ++i)
                s[i] = (byte)i;
        }

        public override Task InitializeAsync() {
            var s = stream.GetSpan();
            for (var i = 0; i < s.Length; ++i)
                if (s[i] != (byte)i)
                    throw new NotImplementedException("Producer's memory was corrupt.");
            return base.InitializeAsync();
        }

        public async Task<int> FillAsync(ITransferBuffer buffer)
        {
            var ret = Math.Min(Parameters.DataLength - buffer.Sequence * State.BlockSize, buffer.Memory.Length);
            if (ret <= 0)
                ret = 0;

            Interlocked.Add(ref bytesTransferred, ret);
            if (Parameters.SimulateIOError == 'L' && bytesTransferred * 3 >= Parameters.DataLength)
                InjectIOException("FillAsync");

            if (ret > 0) {
                var src = stream.Memory.Slice(buffer.Sequence * State.BlockSize, ret);
                src.CopyTo(buffer.Memory);

                // Corrupt the outgoing block; private copy is unchanged.
                if (Parameters.SimulateCorruption && bytesTransferred == Parameters.DataLength)
                    ++buffer.Memory.Span[ret - 1];

                await DelayAsync(ret);
            }
            return ret;
        }
    }

    internal sealed class Consumer : SyntheticGenerator, ITransferConsumer
    {
        // Must be reset on each iteration.
        public override Task InitializeAsync() {
            stream.GetSpan().Fill(0xFF);
            return base.InitializeAsync();
        }

        public async Task DrainAsync(ITransferBuffer buffer)
        {
            var offset = buffer.Sequence * State.BlockSize;
            var block = stream.Memory[offset..];
            buffer.Data.CopyTo(block);
            Interlocked.Add(ref bytesTransferred, buffer.Data.Length);

            // Corrupt private copy, in a different way than producer.
            if (Parameters.SimulateCorruption && bytesTransferred == Parameters.DataLength)
                --stream.GetSpan()[bytesTransferred - 1];

            if (Parameters.SimulateIOError == 'L' && bytesTransferred * 3 >= Parameters.DataLength)
                InjectIOException("DrainAsync");

            await DelayAsync(buffer.Data.Length);
        }
    }
}
