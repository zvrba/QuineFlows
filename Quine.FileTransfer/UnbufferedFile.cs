using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Quine.FileTransfer;

/// <summary>
/// Common implementation for file reader and writer.  Uses unbuffered ("direct") file I/O.
/// </summary>
/// <seealso cref="Reader"/>
/// <seealso cref="Writer"/>
/// <seealso cref="IFileStreamOpenStrategy"/>
public abstract class UnbufferedFile : ITransferWorker
{
    private FileStream stream = null!;
    private SafeFileHandle handle = null!;
    private int sequence;
    private long size;

    /// <summary>
    /// Fully-qualified path to the file.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when trying to set a value that is not a fully-qualified path.</exception>
    /// <exception cref="InvalidOperationException">The path cannot be changed during execution.</exception>
    public string FilePath {
        get => _FilePath;
        set {
            if (!Path.IsPathFullyQualified(value))
                throw new ArgumentException("FilePath must be fully qualified.");
            if (stream is not null)
                throw new InvalidOperationException($"Cannot change {nameof(FilePath)} during execution.");
            _FilePath = value;
        }
    }
    private string _FilePath = null!;

    /// <inheritdoc/>
    public TransferStateMachine State { get; set; } = null!;

    /// <summary>
    /// File I/O currently supports only serial operation.
    /// </summary>
    public int MaxConcurrency => 1;

    private protected abstract FileStream OpenFile();

    /// <inheritdoc/>
    public Task InitializeAsync() {
        Trace.Assert(stream is null, "Stream not released after the previous use.");
        stream = OpenFile();
        handle = stream.SafeFileHandle;
        sequence = 0;
        size = 0;
        Debug.Assert(!handle.IsClosed);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<byte[]?> FinalizeAsync(ITransferHasher? transferHasher, ITransferBuffer buffer) {
        if (stream is null)
            return null;

        byte[]? hash = null;
        try {

            if (stream.CanWrite) {
                RandomAccess.FlushToDisk(handle!);
                RandomAccess.SetLength(handle, size);
            }

            if (transferHasher is not null)
                hash = await ComputeVerificationHashAsync(transferHasher, buffer);

            return hash;
        }
        finally {
            stream?.Dispose();
            stream = null!;
            Trace.Assert(handle is null || handle.IsClosed);
        }
    }

    private async Task<byte[]?> ComputeVerificationHashAsync(ITransferHasher hasher, ITransferBuffer buffer) {
        long size = 0;
        loop:
        var len = await RandomAccess.ReadAsync(handle, buffer.Memory, size, State.CancellationToken);
        size += len;
        if (len > 0)
            hasher.Append(buffer.Memory[..len].Span);
        if (len == buffer.Memory.Length)
            goto loop;
        return hasher.GetHashAndReset();
    }

    /// <summary>
    /// File reader (producer) in a transfer operation.
    /// </summary>
    public class Reader : UnbufferedFile, ITransferProducer
    {
        private protected override FileStream OpenFile() => IFileStreamOpenStrategy.Default.OpenRead(FilePath);

        /// <inheritdoc/>
        public async Task<int> FillAsync(ITransferBuffer buffer) {
            Trace.Assert(buffer.Sequence == this.sequence, "Invalid sequence number for serial reader.");
            var offset = Interlocked.Add(ref size, buffer.Memory.Length) - buffer.Memory.Length;
            var ret = await RandomAccess.ReadAsync(handle, buffer.Memory, offset, State.CancellationToken);
            ++sequence;
            return ret;
        }
    }

    /// <summary>
    /// File writer (consumer) in a transfer operation.
    /// </summary>
    public class Writer : UnbufferedFile, ITransferConsumer
    {
        private protected override FileStream OpenFile() => IFileStreamOpenStrategy.Default.OpenWrite(FilePath);

        // NB! Writing the complete memory buffer (instead of up to valid data length) is NOT a bug!  With unbuffered IO,
        // the size of the last block might not be a multiple required by the filesystem.  Therefore we always write the
        // complete block and truncate the file to correct length in FinalizeAsync.
        /// <inheritdoc/>
        public async Task DrainAsync(ITransferBuffer buffer) {
            Trace.Assert(buffer.Sequence == this.sequence, "Invalid sequence number for serial writer.");
            var offset = Interlocked.Add(ref size, buffer.Data.Length) - buffer.Data.Length;
            await RandomAccess.WriteAsync(handle, buffer.Memory, offset, State.CancellationToken);
            ++sequence;
        }


    }
}
