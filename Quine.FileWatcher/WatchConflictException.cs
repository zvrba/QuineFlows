using System;
using System.Collections.Generic;
using Quine.HRCatalog;

namespace Quine.FileWatcher;

/// <summary>
/// Thrown when conflicting child is attempted added to a watch node.  The path properties DO NOT include
/// the root node's path.
/// </summary>
/// <seealso cref="WatchNode.MakeRoot"/>
/// <seealso cref="WatchNode.MakeChild(Quine.Schemas.Core.PathComponents, Quine.FileWatcher.WatchParameterParser, object)"/>
public class WatchConflictException : Exception, IQHException
{
    internal WatchConflictException
        (
        QHMessage hMessage,
        string conflictingPath,
        IReadOnlyCollection<string> existingPaths
        ) : base(hMessage.Format())
    {
        HResult = hMessage.HResult;
        ConflictingPath = conflictingPath;
        ExistingPaths = existingPaths;
    }

    /// <summary>
    /// Conflicting node that was attempted to be added.
    /// </summary>
    public string ConflictingPath { get; }

    /// <summary>
    /// Collection of all existing paths that conflict with <see cref="ConflictingPath"/>.
    /// </summary>
    public IReadOnlyCollection<string> ExistingPaths { get; }
}

