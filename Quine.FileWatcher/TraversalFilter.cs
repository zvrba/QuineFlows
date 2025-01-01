using System;

namespace Quine.FileWatcher;

/// <summary>
/// Used as argument to <see cref="TraversalFilter"/>.
/// </summary>
[Flags]
public enum TraversalFilterInstruction
{
    /// <summary>
    /// The visited directory is discarded, i.e., it is neither recursed into nor is it added to the result list.
    /// </summary>
    Discard = 0,

    /// <summary>
    /// If set, the visited directory is added to the result list.
    /// </summary>
    Accept = 1,

    /// <summary>
    /// If set, the visited directory is recursed into.
    /// </summary>
    Recurse = 2
}

/// <summary>
/// Delegate type used by <see cref="TraversalVisitor"/>.
/// </summary>
/// <param name="path">Path being visited.</param>
/// <returns>
/// A bitmask value that affects further traversal and whether the directory is included in the returned list.
/// See documentation for <see cref="TraversalFilterInstruction"/>.  The filter must always return <c>Discard</c> if
/// <c>path == PathComponents.Empty</c>.
/// </returns>
public delegate TraversalFilterInstruction TraversalFilter(Schemas.Core.PathComponents path);

