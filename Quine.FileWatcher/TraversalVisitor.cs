using System;
using System.Collections.Generic;
using System.IO;
using Quine.HRCatalog;

namespace Quine.FileWatcher;

/// <summary>
/// Provides methods for traversing the filesystem.
/// </summary>
public class TraversalVisitor
{
    /// <summary>
    /// Don't access this directly; use <see cref="ErrorContext"/>.
    /// </summary>
    private IErrorContext _errorContext;

    /// <summary>
    /// Traversal filter, used to skip particular files and directories.
    /// If <c>null</c>, all entries are reported.
    /// </summary>
    public TraversalFilter Filter { get; set; }

    /// <summary>
    /// Error notifier.  If null, the exception will just be rethrown.
    /// </summary>
    public IErrorContext ErrorContext {
        get {
            if (_errorContext == null)
                _errorContext = IErrorContext.Default.Instance;
            return _errorContext;
        }
        set => _errorContext = value;
    }

    /// <summary>
    /// Subscribe to this event to receive notifications about files that the enumeration discarded.
    /// </summary>
    public event Action<Schemas.Core.PathComponents> PathDiscarded;

    private TraversalFilterInstruction FilterEvaluator(Schemas.Core.PathComponents path) {
        var ret = Filter?.Invoke(path) ?? TraversalFilterInstruction.Accept;
        if (ret == TraversalFilterInstruction.Discard)
            PathDiscarded?.Invoke(path);
        return ret;
    }

    /// <summary>
    /// Enumerates directories in <paramref name="directory"/> as determined by <see cref="Filter"/>.
    /// Note that this method does NOT return <paramref name="directory"/> itself.
    /// The filter must return <see cref="TraversalFilterInstruction.Recurse"/> flag in order to recurse
    /// into subdirectories.
    /// </summary>
    /// <param name="directory">
    /// Top-level directory from which to start the traversal.  Must be an absolute path.
    /// </param>
    /// <returns>A list of all visited directories (their full paths) as determined by the filter.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is not absolute.</exception>
    public IEnumerable<Schemas.Core.PathComponents> GetDirectories(Schemas.Core.PathComponents directory) {
        QHEnsure.State(directory.IsAbsolute);
        
        var bfsq = new Queue<Schemas.Core.PathComponents>(Filter != null ? 64 : 2);
        bfsq.Enqueue(directory);
        while (bfsq.TryDequeue(out var dirpath)) {
            var dirs = GetEntries(dirpath, Directory.GetDirectories);
            if (dirs == null)
                continue;

            foreach (var subdir in dirs) {
                var pc = GetPathComponents(subdir);
                var insn = FilterEvaluator(pc);
                if (insn.HasFlag(TraversalFilterInstruction.Accept))
                    yield return pc;
                if (insn.HasFlag(TraversalFilterInstruction.Recurse))
                    bfsq.Enqueue(pc);
            }
        }
    }

    /// <summary>
    /// Enumerates files in <paramref name="directory"/>.
    /// The enumeration honours <c>Accept</c> flag returned by <see cref="Filter"/>.  Recursion is determined
    /// by <paramref name="recurse"/> parameter.
    /// </summary>
    /// <param name="directory">Directory to traverse. Must be an absolute path.</param>
    /// <param name="recurse">If false, returnes files only from the given directory.  If true, subdirectories are recursed into as well.</param>
    /// <returns>
    /// A list of files (their full paths).  Illegal paths (as governed by <c>PathComponents</c>) are reported through
    /// <see cref="ErrorContext"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is not absolute.</exception>
    public IEnumerable<Schemas.Core.PathComponents> GetFiles(Schemas.Core.PathComponents directory, bool recurse) {
        QHEnsure.State(directory.IsAbsolute);

        var files = GetEntries(directory, GetFilesInDirectory);
        if (files == null)
            yield break;

        foreach (var f in files) {
            var pc = GetPathComponents(f);
            var insn = FilterEvaluator(pc);
            if (insn.HasFlag(TraversalFilterInstruction.Accept))
                yield return pc;
        }

        string[] GetFilesInDirectory(string path) => Directory.GetFiles(path, "*.*", recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
    }

    private Schemas.Core.PathComponents GetPathComponents(string path) {
        try {
            return Schemas.Core.PathComponents.Make(path);
        }
        catch (Schemas.Core.PathFormatException e) 
        when (ErrorContext.ExceptionFilter(new ErrorInfo(this, ErrorCode.IllegalName, path, e), out var e1)) {
            return e1 == null ? Schemas.Core.PathComponents.Empty : throw e1;
        }
    }

    private string[] GetEntries(Schemas.Core.PathComponents path, Func<string, string[]> enumerator) {
        try {
            return enumerator(path.NativeString);
        }
        catch (Exception e) // Catch only if e gets replaced by e1 (which may be null).
        when (ErrorContext.ExceptionFilter(new ErrorInfo(this, ErrorCode.DirectoryEnumerationFailed, path.NormalizedString, e), out var e1)) {
            return e1 == null ? null : throw e1;
        }
    }
}