using System;
using System.Buffers;
using System.Collections.Generic;
using System.Security.Cryptography;

using Quine.HRCatalog;

namespace Quine.Schemas.Core;

/// <summary>
/// Defines protocol for participating in address calculation by <see cref="ContentAddress"/>.
/// </summary>
public interface IContentAddressable
{
    /// <summary>
    /// Adds data to content address calculation when <see cref="IsHashDataProvider"/> is false.
    /// </summary>
    /// <seealso cref="ContentAddress.Add(object[])"/>
    void AddToContentAddress(ref ContentAddress ca);
}

/// <summary>
/// Computes deterministic guid from content data, which can also be used to implement equality.
/// Endianness is taken care of; all data is byte-reversed when run on a big-endian platform.
/// </summary>
/// <remarks>
/// The supported types are primitives, <c>string</c>, <c>DateTime</c>, <c>DateTimeOffset</c>,
/// <c>TimeSpan</c>, <c>Guid</c>, enums (converted to <c>ulong</c> for address computation), <c>byte[]</c>,
/// <c>KeyValuePair{K,V}</c>, <c>IReadOnlyList{V}</c> and types implementing <c>IContentAddressable</c>.
/// Enumerables cannot be nested, and generic parameters must satisfy the same constraints.
/// The computation also distinguishes between a <c>null</c> reference and an empty collection.
/// Dictionaries are not supported because iteration may return elements in non-deterministic order between
/// runs of the same program.
/// </remarks>
public struct ContentAddress
{
    /// <summary>
    /// Namespace bytes used in GUID generation. 
    /// </summary>
    public static readonly byte[] AddressNamespace = new byte[16] {
        0x2C, 0xD5, 0x59, 0xC4, 0x21, 0x74, 0x48, 0xAB, 0xB6, 0xAB, 0x6D, 0x08, 0x75, 0x5B, 0x81, 0xD3
    };

    // Used to encode an empty collection.
    private static readonly byte[] EmptyCollection = new byte[32] {
        0x22, 0x9A, 0xD1, 0x1C, 0x20, 0x99, 0x8A, 0x38, 0xFD, 0xF6, 0xF9, 0x79, 0x1A, 0xD3, 0xF3, 0xF4,
        0x76, 0x06, 0xD5, 0x77, 0x3E, 0x6C, 0x01, 0x43, 0x3E, 0xBF, 0xB3, 0xF7, 0x84, 0x8E, 0x98, 0xA9,
    };

    /// <summary>
    /// Computes content address (a GUID) from given values.  The values, together with the namespace,
    /// are fed to SHA256 and the highest 128 bits are used to form a v5 GUID.
    /// </summary>
    /// <returns>
    /// A guid that uniquely represents the set of values.
    /// </returns>
    public static Guid Get(params object[] values) {
        var instance = new ContentAddress() {
            workspace = ArrayPool<byte>.Shared.Rent(64),
            sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
        };
        try {
            QHEnsure.State(instance.nesting == 0);
            instance.Add(values);
            QHEnsure.State(instance.nesting == 0);
            return instance.Finalize();
        }
        finally {
            ArrayPool<byte>.Shared.Return(instance.workspace);
            instance.workspace = null;
            instance.sha256.Dispose();
            instance.sha256 = null;
        }
    }

    private byte[] workspace;
    private IncrementalHash sha256;
    private int count;
    private int nesting;

    /// <summary>
    /// Visitor method used by <see cref="IContentAddressable"/> for adding values to existing computation.
    /// </summary>
    /// <param name="values"></param>
    public void Add(params object[] values) {
        ++nesting;
        if (workspace is null || sha256 is null)
            throw new ObjectDisposedException(nameof(ContentAddress));
        AddPrivate((IReadOnlyList<object>)values);
        --nesting;
    }

    private void AddPrivate<V>(IReadOnlyList<V> values) {
        if (typeof(System.Collections.IEnumerable).IsAssignableFrom(typeof(V)))
            throw new InvalidOperationException("Nested enumerables cannot be processed by IContentAddressable.");

        // Adding nesting and count helps distinguish the structure
        {
            Span<byte> nc = stackalloc byte[8];
            BitConverter.TryWriteBytes(nc, nesting);
            BitConverter.TryWriteBytes(nc.Slice(4), values.Count);
            sha256.AppendData(nc);
        }

        // Explicit loop to avoid IEnumerable<IEnumerable<object>> which is disallowed below.
        for (int i = 0; i < values.Count; ++i) {
            // Resolve possible ambiguity; IContentAddressable takes precedence.
            switch (values[i]) {
            case null:
                AddNullToHash();
                break;
            case IContentAddressable ca:
                AddPrivate(ca);
                break;
            default:
                AddPrivate((dynamic)values[i]); // May recursively dispatch to self
                break;
            }
        }
    }

    private void AddNullToHash() {
        Array.Fill(workspace, (byte)0, 0, 16);
        AddWorkToHash(16);
    }

    // Workspace contains the raw value to hash.  Append the value's current index.
    private void AddWorkToHash(int size) {
        BitConverter.TryWriteBytes(workspace.AsSpan(size), count++);
        if (!BitConverter.IsLittleEndian) {
            Array.Reverse(workspace, 0, size);
            Array.Reverse(workspace, size, sizeof(int));
        }
        sha256.AppendData(workspace, 0, size + sizeof(int));
    }

    private Guid Finalize() {
        Array.Fill(workspace, (byte)0);
        var hash = sha256.GetHashAndReset();
        Array.Copy(AddressNamespace, workspace, AddressNamespace.Length);
        Array.Copy(hash, 0, workspace, 16, hash.Length);

        var g = SHA256.HashData(workspace);
        Array.Resize(ref g, 16);
        g[6] &= 0x0F; g[6] |= 0x50;   // Version 5
        g[8] &= 0x3F; g[8] |= 0x80;   // IETF variant
        return Guids.FromBytesBE(g, false);
    }

    private void AddPrivate(IContentAddressable x) => x.AddToContentAddress(ref this);

    private void AddPrivate(byte[] x) { sha256.AppendData(x); AddWorkToHash(0); }
    private void AddPrivate(bool x) { BitConverter.TryWriteBytes(workspace, x); AddWorkToHash(sizeof(bool)); }
    private void AddPrivate(sbyte x) { workspace[0] = (byte)x; AddWorkToHash(1); }
    private void AddPrivate(byte x) { workspace[0] = x; AddWorkToHash(1); }
    private void AddPrivate(char x) { BitConverter.TryWriteBytes(workspace, x); AddWorkToHash(sizeof(char)); }
    private void AddPrivate(short x) { BitConverter.TryWriteBytes(workspace, x); AddWorkToHash(sizeof(short)); }
    private void AddPrivate(ushort x) { BitConverter.TryWriteBytes(workspace, x); AddWorkToHash(sizeof(ushort)); }
    private void AddPrivate(int x) { BitConverter.TryWriteBytes(workspace, x); AddWorkToHash(sizeof(int)); }
    private void AddPrivate(uint x) { BitConverter.TryWriteBytes(workspace, x); AddWorkToHash(sizeof(uint)); }
    private void AddPrivate(float x) { BitConverter.TryWriteBytes(workspace, x); AddWorkToHash(sizeof(float)); }
    private void AddPrivate(long x) { BitConverter.TryWriteBytes(workspace, x); AddWorkToHash(sizeof(long)); }
    private void AddPrivate(ulong x) { BitConverter.TryWriteBytes(workspace, x); AddWorkToHash(sizeof(ulong)); }
    private void AddPrivate(double x) { BitConverter.TryWriteBytes(workspace, x); AddWorkToHash(sizeof(double)); }
    private void AddPrivate(Enum x) => AddPrivate(System.Convert.ToUInt64(x));
    private void AddPrivate(DateTime x) => AddPrivate(x.Ticks);
    private void AddPrivate(TimeSpan x) => AddPrivate(x.Ticks);
    private void AddPrivate(Guid x) { x.TryWriteBytes(workspace); AddWorkToHash(16); }    // Guid impl takes care of endianness.
    private void AddPrivate(DateTimeOffset x) { AddPrivate(x.Ticks); AddPrivate(x.Offset.Ticks); }
    private void AddPrivate(string x) => AddString(x, false);

    private void AddPrivate<TStringTraits>(NormalString<TStringTraits> x) where TStringTraits : struct, INormalStringTraits {
        if (x.IsNull) AddNullToHash();
        else AddString(x.Value,
            StringComparer.IsWellKnownOrdinalComparer(
                TStringTraits.StringComparer, out var caseinsensitive)
            && caseinsensitive);
    }

    private void AddString(string x, bool caseinsensitive) {
        int i = 0, wl;
        do {
            wl = 0;
            while (i < x.Length && wl + 2 < workspace.Length) {
                var ch = !caseinsensitive ? x[i] : char.ToLowerInvariant(x[i]);
                workspace[wl++] = (byte)(ch & 0xFF);
                workspace[wl++] = (byte)(ch >> 8);
                ++i;
            }
            sha256.AppendData(workspace, 0, wl);
        } while (i < x.Length);
        AddWorkToHash(0);
    }
}
