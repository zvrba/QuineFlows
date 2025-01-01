using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Quine.FileWatcher;

/// <summary>
/// Monitors file or directory for changes in size.  The equality is defined in terms of the full path of  <see cref="Entry"/>,
/// i.e., the two instances are equal if they monitor the same object.
/// </summary>
abstract class SizeMonitor : IEquatable<SizeMonitor>
{
    /// <summary>
    /// Creates an instance of <c>SizeMonitor</c>, specialized for monitoring directories or files.
    /// </summary>
    /// <param name="path">
    /// Absolute path; must point either to a file or directory.
    /// </param>
    /// <returns>
    /// An valid instance or null if <paramref name="path"/> does not point to an existing file or directory.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="path"/> is not absolute.
    /// </exception>
    public static SizeMonitor Create(Schemas.Core.PathComponents path) {
        if (!path.IsAbsolute)
            throw new ArgumentException("Path must be absolute.", nameof(path));
        if (File.Exists(path.NativeString))
            return new FileSizeMonitor(new FileInfo(path.NativeString));
        if (Directory.Exists(path.NativeString))
            return new DirectorySizeMonitor(new DirectoryInfo(path.NativeString));
        return null;
    }

    /// <summary>
    /// File or directory being watched.
    /// </summary>
    public FileSystemInfo Entry { get; }

    /// <inheritdoc/>
    public Schemas.Core.PathComponents Path { get; }

    /// <inheritdoc/>
    public int FileCount => previous.count;

    /// <inheritdoc/>
    public long TotalSize => previous.size;

    (int count, long size) previous;

    private SizeMonitor(FileSystemInfo entry) {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        Path = Schemas.Core.PathComponents.Make(Entry.FullName);
        previous = Refresh(true);
    }

    /// <inheritdoc/>
    public bool SizeChanged(out Exception exn) {
        exn = null;
        try {
            var current = Refresh(false);
            var changed = current != previous;
            previous = current;
            return changed;
        }
        catch (Exception e) {
            previous = (0, 0L);
            exn = e;
            return true;
        }
    }

    /// <summary>
    /// Calculates the current size of the object referenced by <see cref="Entry"/>.
    /// </summary>
    /// <param name="firstTime">True when called for the first time (from ctor).  False on all subsequent invocations.</param>
    /// <returns>
    /// A tuple consisting of the number of files and their total size.
    /// </returns>
    protected abstract (int count, long size) Refresh(bool firstTime);

    public bool Equals([AllowNull] SizeMonitor other) => other != null && other.Entry.FullName == Entry.FullName;
    public override bool Equals(object obj) => Equals(obj as SizeMonitor);
    public override int GetHashCode() => Entry.FullName.GetHashCode();

    /// <summary>
    /// Monitors the size of a single file.
    /// </summary>
    public sealed class FileSizeMonitor : SizeMonitor
    {
        public new FileInfo Entry => (FileInfo)base.Entry;
        public FileSizeMonitor(FileInfo entry) : base(entry) { }

        protected override (int count, long size) Refresh(bool firstTime) {
            if (!firstTime)
                Entry.Refresh();
            return (1, Entry.Length);
        }
    }

    /// <summary>
    /// Monitors the total size and count of files in a directory.
    /// </summary>
    public sealed class DirectorySizeMonitor : SizeMonitor
    {
        TraversalVisitor traversal = new();

        public new DirectoryInfo Entry => (DirectoryInfo)base.Entry;
        public DirectorySizeMonitor(DirectoryInfo entry) : base(entry) { }

        protected override (int count, long size) Refresh(bool firstTime) {
            var files = traversal.GetFiles(Path, true);
            var size = files.Select(x => new FileInfo(x.NativeString))
                .Aggregate((0, 0L), (s, fi) => (s.Item1 + 1, s.Item2 + fi.Length));
            return size;
        }
    }
}
