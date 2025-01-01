using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

using Quine.HRCatalog;

namespace Quine.Schemas.Graph;

/// <summary>
/// Uniquely identifies a graph node within a set of unrelated graph runs.
/// Encodes OID-like identifiers (e.g., <c>7.3.11.22.5</c>) into a compact binary format.
/// </summary>
[DataContract(Namespace = XmlNamespaces.Graph)]
public readonly struct TreePathId : IEquatable<TreePathId>
{
    /// <summary>
    /// True for a <c>default</c> instance.
    /// </summary>
    public bool IsNull => _Value is null;

    /// <summary>
    /// Binary representation of the ID.
    /// </summary>
    public ReadOnlySpan<byte> Value => _Value;
    [DataMember(Name = "Value")]
    private readonly byte[] _Value;

    internal TreePathId(int[] path) {
        if (path?.Length > 0) {
            var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms)) {
                for (int i = 0; i < path.Length; ++i)
                    bw.Write7BitEncodedInt(path[i]);
            }
            _Value = ms.ToArray();
        }
    }

    public bool Equals(TreePathId other) => IsNull ? other.IsNull : _Value.SequenceEqual(other._Value);
    public override bool Equals(object obj) => obj is TreePathId tp && tp.Equals(this);
    public override int GetHashCode() => _Value.GetHashCode();
    public static bool operator ==(TreePathId left, TreePathId right) => left.Equals(right);
    public static bool operator !=(TreePathId left, TreePathId right) => !(left == right);
    
    public override string ToString() {
        var sb = new StringBuilder(256);
        sb.Append("{TreePathId`");
        if (_Value is not null) {
            var ms = new MemoryStream(_Value, false);
            using (var br = new BinaryReader(ms)) {
                try {
                    while (true) {
                        var id = br.Read7BitEncodedInt();
                        sb.AppendFormat(".{0}", id);
                    }
                }
                catch (EndOfStreamException) {
                    // All data read.
                }
            }
        }
        sb.Append('}');
        return sb.ToString();
    }
}

/// <summary>
/// Provides a hierarchical id for jobs, messages, etc.
/// </summary>
public interface ITreeIdentity : Core.IIdentity<int>
{
    /// <summary>
    /// Parent/owner of this node, or null.
    /// </summary>
    ITreeIdentity Owner { get; }

    /// <summary>
    /// Provides IDs of all job nodes from the root (1st element) to <c>this</c>.
    /// The byte array is a sequence of integer ids with variable-length encoding (7-bit).
    /// </summary>
    TreePathId PathId { get; }
}

/// <summary>
/// Provides a hook for connecting schema classes with their run-time behavior.
/// </summary>
[DataContract(Namespace = XmlNamespaces.Graph, IsReference = true)]
public abstract class GraphRuntimeHook : ITreeIdentity
{
    /// <summary>
    /// "Local" id of this instance.  This is valid only within the same graph.
    /// </summary>
    [DataMember]
    public int Id { get; private set; }

    /// <inheritdoc/>
    [DataMember]
    public TreePathId PathId { get; private set; }

    // NB! NOT DataMember

    /// <inheritdoc/>
    public ITreeIdentity Owner { get; private set; }

    /// <summary>
    /// The object implementing the actual behavior.
    /// </summary>
    public ITreeIdentity RuntimeObject { get; private set; }

    private protected GraphRuntimeHook() { }

    internal void SetRuntimeObject(ITreeIdentity o) {
        QHEnsure.State(RuntimeObject == null);
        QHEnsure.State(o.PathId == PathId);
        RuntimeObject = o;
    }

    /// <summary>
    /// Sets id and path id on this.
    /// </summary>
    /// <param name="owner">Owning parent of <c>this</c>; may be null.</param>
    /// <param name="id">"Local" id to set on <c>this</c>.</param>
    /// <remarks>
    /// Derived classes should override this method if they contain any children so that child treeids are assigned correctly.
    /// </remarks>
    public virtual void SetId(ITreeIdentity owner, int id) {
        Owner = owner;
        Id = id;

        var path = new int[Depth()];
        ITreeIdentity node = this;
        for (var i = path.Length - 1; i >= 0; --i, node = node.Owner)
            path[i] = node.Id;
        PathId = new(path);
    }

    int Depth() {
        int d = 0;
        for (ITreeIdentity node = this; node != null; ++d, node = node.Owner) ;
        return d;
    }
}
