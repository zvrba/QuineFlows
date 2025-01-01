using System;
using System.Security.Cryptography;

namespace Quine.FileTransfer;

/// <summary>
/// Implements <see cref="ITransferHasher"/> by a user-specified cryptographic hash.
/// </summary>
public sealed class CryptographicTransferHash : ITransferHasher, IDisposable
{
    private readonly HashAlgorithmName algorithmName;
    private IncrementalHash h;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="algorithmName">Algorithm to use to compute the hash.</param>
    public CryptographicTransferHash(HashAlgorithmName algorithmName) {
        this.algorithmName = algorithmName;
        this.h = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (h is null)
            return;
        h.Dispose();
        h = null!;
    }

    /// <inheritdoc/>
    public ITransferHasher Clone() => new CryptographicTransferHash(algorithmName);

    /// <inheritdoc/>
    public void Append(ReadOnlySpan<byte> data) => h.AppendData(data);

    /// <inheritdoc/>
    public byte[] GetHashAndReset() => h.GetHashAndReset();
}

/// <summary>
/// Implements XXHash64 algorithm.
/// </summary>
public sealed class XX64TransferHash : ITransferHasher
{
    private System.IO.Hashing.XxHash64 h;

    /// <inheritdoc/>
    public XX64TransferHash() {
        h = new();
    }

    /// <inheritdoc/>
    public ITransferHasher Clone() => new XX64TransferHash();

    /// <inheritdoc/>
    public void Append(ReadOnlySpan<byte> data) {
        ObjectDisposedException.ThrowIf(h is null, this);
        h.Append(data);
    }

    /// <inheritdoc/>
    public byte[] GetHashAndReset() {
        ObjectDisposedException.ThrowIf(h is null, this);
        return h.GetHashAndReset();
    }
}
