#if false
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Quine.Graph;
using Quine.HRCatalog;
using Quine.Schemas.Core;

namespace Quine.FileWatcher;

/// <summary>
/// Enumerates files on the magazine and computes the hash.
/// </summary>
public sealed class MagazineContent : IContentAddressable, IErrorContext
{

    /// <summary>
    /// File and directory names that are always ignored during traversal.  When the traversal encounters a file or directory
    /// whose name is in this list, it will treat it as if <see cref="Filter"/> returned <c>Discard</c> flag.
    /// </summary>
    /// <seealso cref="AlwaysIgnoredFilesFilter(PathComponents)"/>
    public static readonly string[] AlwaysIgnoredFiles = new string[] {
        ".DS_Store",
        "._.DS_Store",
        ".Spotlight-V100",
        ".apDisk",
        ".VolumeIcons.icns",
        ".fseventsd",
        ".Trash",
        ".Trashes",
        ".temporaryItems",
        "$RECYCLE.BIN",
        "System Volume Information"
    };

    void IContentAddressable.AddToContentAddress(ref ContentAddress ca) => ca.Add(Elements);

    /// <summary>
    /// Enumerates all content in <paramref name="inputPath"/>.  Certain well-known files are always ignored, regardless
    /// of whether <paramref name="traversalFilter"/> is provided.
    /// </summary>
    /// <param name="eventSource">Node for logging messages.</param>
    /// <param name="inputPath">Starting point for the enumeration; may be file or directory.  Must be an absolute path.</param>
    /// <param name="traversalFilter">
    /// Determines which files to accept or discard.  May be set to <c>null</c> to accept all files.</param>
    /// <returns>
    /// An valid instance with populated properties.
    /// </returns>
    /// <seealso cref="AlwaysIgnoredFiles"/>
    public static MagazineContent Create
        (
        INodeEventSource eventSource,
        string inputPath,
        TraversalFilter traversalFilter
        )
    {
        var ret = new MagazineContent(QHEnsure.NotNull(eventSource), traversalFilter);
        ret.Collect(QHEnsure.Value(inputPath, Path.IsPathFullyQualified(inputPath)));
        ret._Elements.Sort(new ElementComparisons());
        return ret;
    }

    private readonly INodeEventSource eventSource;
    private readonly TraversalFilter traversalFilter;

    /// <summary>
    /// Common path prefix for all files on the magazine.  This may be empty in certain cases.
    /// </summary>
    public PathComponents RootPrefix { get; private set; }

    /// <summary>
    /// True if the input to <see cref="Create(INodeEventSource, FileInfo, TraversalFilter)"/> was a single file;
    /// false if it was directory.
    /// </summary>
    public bool InputIsFile { get; private set; }

    /// <summary>
    /// One element per file, containing its file info and relativized path.
    /// Includes empty (leaf) directories.
    /// The list is sorted by name so that hash computation is deterministic.
    /// </summary>
    public IReadOnlyList<Element> Elements => _Elements;
    private readonly List<Element> _Elements;

    /// <summary>
    /// Empty directories.
    /// </summary>
    public IReadOnlySet<Element> LeafDirectories => _LeafDirectories;
    private readonly HashSet<Element> _LeafDirectories;

    /// <summary>
    /// Discarded paths (absolute).
    /// </summary>
    public IReadOnlyList<PathComponents> DiscardedPaths => _DiscardedPaths;
    private readonly List<PathComponents> _DiscardedPaths;

    private MagazineContent(INodeEventSource eventSource, TraversalFilter traversalFilter) {
        this.eventSource = eventSource;
        this.traversalFilter = traversalFilter;
        _Elements = new(64);
        _LeafDirectories = new(new ElementComparisons());
        _DiscardedPaths = new(16);
    }

    private void Collect(string inputPath) {
        var inputDirectoryInfo = new DirectoryInfo(inputPath);
        var inputFileInfo = new FileInfo(inputPath);

        // Initialization: determine input type and root prefix.
        if (inputDirectoryInfo.Exists) {
            var parent = inputDirectoryInfo.Parent;
            if (parent != null) RootPrefix = PathComponents.Make(parent.FullName);
            else RootPrefix = PathComponents.Empty;
            InputIsFile = false;
        }
        else if (inputFileInfo.Exists) {
            var parent = inputFileInfo.Directory;
            if (parent != null) RootPrefix = PathComponents.Make(parent.FullName);
            else RootPrefix = PathComponents.Empty;
            InputIsFile = true;

            var pc = PathComponents.Make(inputFileInfo.FullName);
            if (ActualFilter(pc) == TraversalFilterInstruction.Discard) {
                LogDiscardedPath(pc);
                return;
            }
            _Elements.Add(new(RootPrefix, inputFileInfo));
            return;

        }
        else {
            eventSource.Log(QHNucleus.Filesystem.C_InvalidFileInfoType, null, inputPath);
            throw new ArgumentException("Input path does not point to a valid file or directory.", nameof(inputPath));
        }

        // Complicated case: directory.  The implementation is suboptimal (first we traverse directories, then files
        // in each directory), but it 1) is simpler, 2) allows for more fine-grained error handling.
        var tv = new TraversalVisitor() { Filter = ActualFilter, ErrorContext = this };
        try {
            tv.PathDiscarded += LogDiscardedPath;

            var ipc = PathComponents.Make(inputDirectoryInfo.FullName);
            foreach (var d in tv.GetDirectories(ipc).Prepend(ipc)) {    // Prepend because GetDirectories doesn't return ipc
                int fileCount = 0;
                foreach (var f in tv.GetFiles(d, false)) {
                    _Elements.Add(new(RootPrefix, new(f.NativeString)));
                    ++fileCount;
                }
                if (fileCount == 0) {
                    // This is the case when a directory only contains other directory => NOT a leaf directory.
                    var dirs = Directory.GetDirectories(d.NativeString);
                    if (dirs.Length == 0) {
                        var e = new Element(RootPrefix, new(d.NativeString));
                        _Elements.Add(e);
                        _LeafDirectories.Add(e);
                    }
                }
            }
        }
        finally {
            tv.PathDiscarded -= LogDiscardedPath;
        }
    }

    /// <summary>
    /// Helper method for using <see cref="TraversalVisitor"/>.  Discards all paths that are "known" to be discarded.
    /// </summary>
    /// <returns>Either discard or accept+recurse flags.</returns>
    public static TraversalFilterInstruction AlwaysIgnoredFilesFilter(PathComponents path) {
        if (path.IsEmpty)
            return TraversalFilterInstruction.Discard;

        var fn = path[-1];

        if
        (
            fn.StartsWith("._", StringComparison.OrdinalIgnoreCase) ||
            fn.StartsWith(".QI-", StringComparison.OrdinalIgnoreCase) ||
            fn.EndsWith(".mhl", StringComparison.OrdinalIgnoreCase)
        )
            return TraversalFilterInstruction.Discard;

        if (AlwaysIgnoredFiles.Contains(fn))
            return TraversalFilterInstruction.Discard;

        return TraversalFilterInstruction.Accept | TraversalFilterInstruction.Recurse;
    }

    private TraversalFilterInstruction ActualFilter(PathComponents path) {
        if (AlwaysIgnoredFilesFilter(path) == TraversalFilterInstruction.Discard)
            return TraversalFilterInstruction.Discard;

        // Provided filter: DO NOT set recurse flag as some well-known directories trees may be ignored.
        if (traversalFilter != null)
            return traversalFilter(path);

        return TraversalFilterInstruction.Accept | TraversalFilterInstruction.Recurse;
    }

    void LogDiscardedPath(PathComponents path) {
        eventSource.RaiseTraceEvent(new QHNotificationEvent(QHNucleus.Filesystem.I_MagazineFileDiscardedByFilter, null, path.NormalizedString));
        _DiscardedPaths.Add(path);
    }

    Exception IErrorContext.Accept(in ErrorInfo errorInfo) {
        eventSource.RaiseTraceEvent(new QHNotificationEvent(QHNucleus.Filesystem.E_MagazineEnumerationError, errorInfo.Exception,
            errorInfo.Path, errorInfo.ErrorCode));
        return null;
    }

    // TODO: Should include creation time, but it's unreliable on some unix filesystems
    public sealed class Element : IContentAddressable {
        void IContentAddressable.AddToContentAddress(ref Quine.Schemas.Core.ContentAddress ca) => ca.Add(
            RelativePath.NormalizedString,
            !FileInfo.Attributes.HasFlag(FileAttributes.Directory) ? FileInfo.Length : -1L
        );
        
        internal Element(PathComponents rootPrefix, FileInfo fi) {
            this.FileInfo = fi;
            this.RelativePath = PathComponents.Make(fi.FullName).RemovePrefix(rootPrefix);
        }

        public readonly FileInfo FileInfo;
        public readonly PathComponents RelativePath;

        public override int GetHashCode() => RelativePath.GetHashCode();
    }

    private struct ElementComparisons : IEqualityComparer<Element>, IComparer<Element>
    {
        public int Compare(Element x, Element y) {
            var r = string.CompareOrdinal(x.RelativePath.NormalizedString, y.RelativePath.NormalizedString);
            return QHEnsure.Value(r, ReferenceEquals(x,y) || r != 0);  // No two paths shall be equal in this collection.
        }

        public bool Equals(Element x, Element y) => x.RelativePath == y.RelativePath;

        public int GetHashCode(Element obj) => obj.RelativePath.GetHashCode();
    }
}
#endif