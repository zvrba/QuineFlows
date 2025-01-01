using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Quine.FileTransfer;

internal sealed class FileHasher : ITransferConsumer, IDisposable
{
    private int sequence;
    private bool isDisposed;

    internal FileHasher(ITransferHasher hasher) {
        this.TransferHasher = hasher;
    }

    public void Dispose() {
        if (TransferHasher is IDisposable d)
            d.Dispose();
        isDisposed = true;
    }

    /// <summary>
    /// Hasher instance used for hash computation.
    /// </summary>
    internal ITransferHasher TransferHasher { get; }

    /// <summary>
    /// Value of the incrementally computed hash, or <c>null</c> if errors occurred during computation.
    /// </summary>
    public byte[]? Hash { get; private set; }

    /// <inheritdoc/>
    public TransferStateMachine State { get; set; } = null!;

    /// <inheritdoc/>
    public int MaxConcurrency => 1;

    /// <summary>
    /// Implements <see cref="ITransferWorker.InitializeAsync"/>.  The method is virtual so that the derived class
    /// can create the actual hash object.
    /// </summary>
    /// <returns></returns>
    public Task InitializeAsync() {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        Hash = null;
        sequence = 0;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DrainAsync(ITransferBuffer buffer) {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        Trace.Assert(buffer.Sequence== this.sequence, "Invalid sequence number.");
        State.CancellationToken.ThrowIfCancellationRequested();
        ++this.sequence;
        TransferHasher.Append(buffer.Data.Span);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<byte[]?> FinalizeAsync(ITransferHasher? transferHasher, ITransferBuffer buffer) {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        Debug.Assert(transferHasher is null);
        Hash = TransferHasher.GetHashAndReset();    // Must always reset the hash.
        if (State.IsFaulted)                        // Set hash to null in case of error.
            Hash = null;
        return Task.FromResult<byte[]?>(null);
    }
}
