using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Quine.FileTransfer;

/// <summary>
/// Data buffer shared between producer and consumers.
/// </summary>
public interface ITransferBuffer
{
    /// <summary>
    /// Assumed largest possible disk sector size.  Direct file IO needs all IO buffers to be aligned on sector size and be
    /// a multiple of sector size.
    /// </summary>
    const int SectorSize = 4096;

    /// <summary>
    /// The fixed-sized memory block into which the producer writes data.
    /// </summary>
    Memory<byte> Memory { get; }

    /// <summary>
    /// Sequence number in the file of this buffer.
    /// It determines the offset (in multiples of <see cref="TransferStateMachine.BlockSize" />) in the file
    /// at which the data starts.
    /// </summary>
    int Sequence { get; }

    /// <summary>
    /// Memory block from which the consumers read data.  It is a  prefix of <see cref="Memory"/>, i.e., it may have shorter length.
    /// This occurs only for the last block of the file.
    /// </summary>
    Memory<byte> Data { get; }
}

internal unsafe sealed class AlignedBuffer : MemoryManager<byte>, ITransferBuffer
{
    private readonly byte* start;
    private readonly int length;

    internal AlignedBuffer(TransferBufferPool owner, int length) {
        this.start = (byte*)NativeMemory.AlignedAlloc((nuint)length, ITransferBuffer.SectorSize);
        this.length = length;
        this._Owner = owner;
        Trace.Assert(((nuint)this.start & (nuint)(ITransferBuffer.SectorSize - 1)) == 0);
    }

    /// <summary>
    /// Finalizer: releases the native memory block.
    /// </summary>
    ~AlignedBuffer() => Dispose(false);

    public int Sequence => _Sequence;
    public Memory<byte> Data => _Data;

    internal readonly TransferBufferPool _Owner;
    internal bool IsDisposed { get; private set; }
    internal int _Sequence;
    internal Memory<byte> _Data;
    internal int _UseCount;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing) {
        if (IsDisposed)
            return;
        NativeMemory.AlignedFree(start);
        IsDisposed = true;
    }

    /// <inheritdoc/>
    public override Span<byte> GetSpan() {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return new Span<byte>(start, length);
    }

    // Pin/Unpin do nothing; native memory is already pinned.

    /// <inheritdoc/>
    public override MemoryHandle Pin(int elementIndex = 0) {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        return new(start + elementIndex);
    }

    /// <inheritdoc/>
    public override void Unpin() { }
}
