using System;
using System.IO;

namespace Quine.FileTransfer;

/// <summary>
/// Thrown when hash verification fails.
/// </summary>
public sealed class HashVerificationException : IOException
{
    internal HashVerificationException(string message, Exception inner) : base(message, inner) { }

    internal HashVerificationException(byte[] referenceHash, byte[] verificationHash)
        : base("Reference and verification hashes differ.")
    {
        _ReferenceHash = (byte[]?)referenceHash.Clone();
        _VerificationHash = (byte[]?)verificationHash.Clone();
    }

    /// <summary>
    /// Reference hash, computed during the 1st pass through data.
    /// This is a zero-length span if the computation failed.
    /// </summary>
    public ReadOnlySpan<byte> ReferenceHash => _ReferenceHash;
    private byte[]? _ReferenceHash;

    /// <summary>
    /// Verification hash, computed during the 2nd pass through data.
    /// This is a zero-length span if the computation failed.
    /// </summary>
    public ReadOnlyMemory<byte> VerificationHash => _VerificationHash;
    private byte[]? _VerificationHash;
}
