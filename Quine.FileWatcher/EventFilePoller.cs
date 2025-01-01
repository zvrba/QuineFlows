// Temporarily disabled until the cause of frequent "Too many changes" exception is found.
#if false
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

using Quine.Base.Errors;

namespace Quine.Ingest.Nucleus.Fs.Watcher;

/// <summary>
/// Uses <c>FileSystemWatcher</c> to recursively watch for new files in a directory.  Newly created files are
/// delivered only after their size has not changed since the last call to <see cref="Poll"/>.  This class is
/// NOT thread-safe.
/// </summary>
/// <remarks>
/// Only a single poller can watch a directory.
/// </remarks>
class FilePoller : IDisposable
{
    private readonly Graph.IInteractiveProvider interactiveProvider;
    private readonly Schemas.Graph.ITreeIdentity owner;
    private readonly FileSystemWatcher fswatcher;
    private volatile Exception watchException;

    // FsWatcher only places events in stage1. Polling moves files from stage1 to stage2 and checks for stability.
    // Stable files are moved from stage2 to stable. stage1 is the only one being accessed from multiple threads.

    private readonly ConcurrentQueue<FileSystemEventArgs> stage1 = new ConcurrentQueue<FileSystemEventArgs>();
    private readonly HashSet<FileSizeWatcher> stage2 = new HashSet<FileSizeWatcher>();
    private readonly HashSet<Schemas.Core.PathComponents> stable = new HashSet<Schemas.Core.PathComponents>();

    public Schemas.Core.PathComponents WatchedFolder { get; set; }
    public IEnumerable<string> IgnoredFiles { get; }

    /// <summary>
    /// Constructor.  Polling is inactive until <see cref="Enable"/> has been called.
    /// </summary>
    /// <param name="interactiveProvider">Used for sending interactive notifications.</param>
    /// <param name="owner">Used for sending interactive notification; identifies the job owning this poller.</param>
    /// <param name="watchedVolumeRoot">Absolute path to the volume to watch.</param>
    /// <param name="ignoredFiles">List of file names for which to NOT report changes.</param>
    public FilePoller(
        Graph.IInteractiveProvider interactiveProvider,
        Schemas.Graph.ITreeIdentity owner,
        string watchedVolumeRoot,
        IEnumerable<string>  ignoredFiles)
    {
        WatchedFolder = Schemas.Core.PathComponents.Make(watchedVolumeRoot);
        Expect.State(WatchedFolder.IsAbsolute && Path.IsPathFullyQualified(WatchedFolder.NormalizedString),
            WatchedFolder, "Watchfolder target must be an absolute path.");
        IgnoredFiles = Expect.NotNull(ignoredFiles, nameof(ignoredFiles));

        this.interactiveProvider = interactiveProvider;
        this.owner = owner;
        this.fswatcher = new FileSystemWatcher(WatchedFolder.NormalizedString, "*.*") {
            IncludeSubdirectories = true,
            InternalBufferSize = 16384,     // Should be a multiple of 4096
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
        };
        this.fswatcher.Created += FsWatcherCreated;
        this.fswatcher.Error += FsWatcherError;
    }

    public void Dispose() {
        try {
            fswatcher.Dispose();
        }
        catch (Exception e) {
            var m = $"Could not disable file system watcher -- QI should be restarted.\nReasong: {e.Message}";
            interactiveProvider.Send(new Schemas.Graph.LogTraceEvent(owner, TraceEventType.Warning, false, m));
            throw;
        }
    }

    /// <summary>
    /// Returns true if the poller is active.
    /// </summary>
    public bool IsEnabled => fswatcher.EnableRaisingEvents;

    /// <summary>
    /// Enable polling.
    /// </summary>
    public void Enable() {
        fswatcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// The given path will not be reported by following calls to <see cref="Poll"/>.
    /// </summary>
    public void Disable(Schemas.Core.PathComponents p) {
        Expect.Value(WatchedFolder.IsPrefixOf(p), p, "Path is not parented under watchfolder.");
        stable.Add(p);
    }

    /// <summary>
    /// The given path will be reported by following calls to <see cref="Poll"/> if it is re-created.
    /// </summary>
    public void Enable(Schemas.Core.PathComponents p) {
        Expect.Value(WatchedFolder.IsPrefixOf(p), p, "Path is not parented under watchfolder.");
        stable.Remove(p);
    }

    /// <summary>
    /// Enqueues a file obtained out-of-band to be returned by subsequent calls to <see cref="Poll"/>.  Non-existing
    /// files or files on the ignore list are silently ignored.
    /// </summary>
    /// <param name="path">Absolute path under watched folder to eqneueue.</param>
    public void Enqueue(string path) {
        try {
            var fi = new FileInfo(path);
            if (fi.Attributes.HasFlag(FileAttributes.Directory))
                return;
            if (fi.Exists) {
                var fsw = new FileSizeWatcher(fi);
                if (fsw.FilePath.Any(IgnoreFile))
                    return;
                Expect.Value(fsw.FilePath.IsAbsolute, fsw.FilePath, "Can enqueue only absolute paths.");
                Expect.Value(WatchedFolder.IsPrefixOf(fsw.FilePath), fsw.FilePath, "Enqueued file must be under the watched folder.");
                stage2.Add(fsw);
            }
        }
        catch (Exception e) {
            var m = string.Format("Cannot ingest file {0} to stage 1.\nReason: {1}", path, e.Message);
            interactiveProvider.Send(new Schemas.Graph.LogTraceEvent(owner, TraceEventType.Error, false, m));
        }

        bool IgnoreFile(string name) {
            name = name.ToUpperInvariant();
            if (name.StartsWith("._")) return true;
            if (name.StartsWith(".QI")) return true;   // All QI "control" files.
            if (name.EndsWith(".MHL")) return true;
            return IgnoredFiles.Contains(name);
        }
    }

    public IEnumerable<Schemas.Core.PathComponents> Poll() {
        if (watchException != null)
            throw new NotSupportedException("Unhandled FileSystemWatcher error.", watchException);

        // NB! Order: We must first process files that have waited during one cycle before adding new files.
        // Otherwise, a file added by ProcessStage1 could be immediately processed by stage2 w/o waiting for stable size.
        var ret = ProcessStage2().Select(x => x.FilePath);
        ProcessStage1();
        return ret;

        void ProcessStage1() {
            while (stage1.TryDequeue(out var fse))
                Enqueue(fse.FullPath);
        }

        HashSet<FileSizeWatcher> ProcessStage2() {
            var ret = new HashSet<FileSizeWatcher>();
            var removed = new HashSet<FileSizeWatcher>();

            foreach (var fsw in stage2) {
                if (this.stable.Contains(fsw.FilePath))
                    continue;
                try {
                    if (!fsw.LengthChanged()) {
                        ret.Add(fsw);
                        removed.Add(fsw);
                    }
                }
                catch (Exception e) {
                    var m = string.Format("Cannot ingest file {0} to stage 2.\nReason: {1}", fsw.FileInfo.FullName, e.Message);
                    interactiveProvider.Send(new Schemas.Graph.LogTraceEvent(owner, TraceEventType.Error, false, m));
                    removed.Add(fsw);
                }
            }

            removed.UnionWith(ret);
            stage2.ExceptWith(removed);
            return ret;
        }
    }


    private void FsWatcherCreated(object sender, FileSystemEventArgs args) {
        stage1.Enqueue(args);
    }

    private void FsWatcherError(object sender, ErrorEventArgs args) {
        lock (this)
            if (watchException == null) watchException = args.GetException() ?? new Exception("(UNKNOWNO)");
    }
}
#endif
