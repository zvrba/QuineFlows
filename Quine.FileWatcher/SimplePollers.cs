using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Quine.HRCatalog;

namespace Quine.FileWatcher;

/// <summary>
/// Recursively watches a directory for new files by polling so that files being written to are delivered only after
/// their size has not changed since the last call to <see cref="Update"/>.  This class is NOT thread-safe.
/// </summary>
public class SimpleDirectoryMonitor
{
    private readonly HashSet<SizeMonitor> stagedFiles = new();
    private readonly HashSet<Schemas.Core.PathComponents> stableFiles = new();
    private readonly HashSet<Schemas.Core.PathComponents> newFiles = new();
    private readonly TraversalVisitor traversal;

    private IErrorContext _errorContext;
    
    /// <summary>
    /// Directory being watched.
    /// </summary>
    public Schemas.Core.PathComponents Directory { get; }

    /// <summary>
    /// All files ever observed.
    /// </summary>
    public IEnumerable<Schemas.Core.PathComponents> AllEntries => stableFiles;

    /// <summary>
    /// New files observed after the last call to <see cref="Update"/>.
    /// </summary>
    public IEnumerable<Schemas.Core.PathComponents> NewEntries => newFiles;

    /// <summary>
    /// Error notifier. If null, the exception will just be rethrown.
    /// </summary>
    public IErrorContext ErrorContext {
        get => _errorContext;
        init => _errorContext = value ?? IErrorContext.Default.Instance;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="directory">Absolute path to the recursively watched directory.</param>
    public SimpleDirectoryMonitor(Schemas.Core.PathComponents directory) {
        QHEnsure.State(directory.IsAbsolute);
        var di = new DirectoryInfo(directory.NativeString);
        QHEnsure.State(di.Exists);
        this.Directory = directory;
        this.traversal = new TraversalVisitor() { Filter = NewFileFilter };
    }

    TraversalFilterInstruction NewFileFilter(Schemas.Core.PathComponents path) =>
        stableFiles.Contains(path) || stagedFiles.Any(x => x.Path == path) ?
        TraversalFilterInstruction.Discard :
        TraversalFilterInstruction.Accept;

    /// <summary>
    /// Refreshes the content of <see cref="AllEntries"/> and <see cref="NewEntries"/>.
    /// </summary>
    public void Update() {
        newFiles.Clear();
        AddStableFiles();
        StageNewFiles();
    }

    private void AddStableFiles() {
        var toRemove = new HashSet<SizeMonitor>();  // Needed because staged files can't be modified from within foreach().
        foreach (var sizeMonitor in stagedFiles) {
            if (!sizeMonitor.SizeChanged(out var exn)) {
                toRemove.Add(sizeMonitor);
                if (stableFiles.Add(sizeMonitor.Path))
                    newFiles.Add(sizeMonitor.Path);
            }
            else if (exn != null) {
                exn = ErrorContext.Accept(new ErrorInfo(this, ErrorCode.SizeRefreshFailed, sizeMonitor.Path.NormalizedString, exn));
                if (exn != null)
                    throw exn;
                toRemove.Add(sizeMonitor);
            }
            // Size changed, but no exception: keep watching, i.e., don't add the monitor to the remove list.
        }
        stagedFiles.ExceptWith(toRemove);
    }

    private void StageNewFiles() {
        foreach (var f in traversal.GetFiles(Directory, true)) {
            try {
                var sm = SizeMonitor.Create(f);
                if (sm != null)
                    stagedFiles.Add(sm);
            }
            catch (Exception e)
            when (ErrorContext.ExceptionFilter(new ErrorInfo(this, ErrorCode.SizeRefreshFailed, f.NormalizedString, e), out var e1)) {
                if (e1 != null)
                    throw e1;
            }
        }
    }
}
