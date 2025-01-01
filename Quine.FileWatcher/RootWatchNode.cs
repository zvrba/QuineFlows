using System;
using System.Collections.Generic;
using System.Linq;

namespace Quine.FileWatcher;

sealed class RootWatchNode : WatchNode
{
    internal RootWatchNode() : base(null, IErrorContext.Default.Instance, Schemas.Core.PathComponents.Empty) { }

    internal RootWatchNode(Schemas.Core.PathComponents pattern, IErrorContext errorContext) : base(null, errorContext, pattern) {
        if (pattern.IsEmpty || !pattern.IsAbsolute)
            throw new ArgumentException("Path must be absolute.", nameof(pattern));
        if (errorContext == null)
            throw new ArgumentNullException(nameof(errorContext));

        var r = new WatchResultEntry[] { new WatchResultEntry(Pattern) };
        Result = new WatchResult() {
            AllEntries = r,
            NewEntries = r  // Must always be considered as "new" due to recursion.
        };
    }

    private protected override bool IsEquivalent(WatchNode other) => false;
    
    // Default Update() and Clone() are fine.

    /// <summary>
    /// Walks the watch tree using <c>this</c> as the error context.
    /// </summary>
    /// <returns>An instance of <see cref="WatchResult"/>.</returns>
    internal WatchResult Walk() {
        Update();
        var leaves = new List<WatchNode>();
        GetLeaves(this, leaves);
        return new WatchResult() {
            AllEntries = leaves.SelectMany(x => x.Result.AllEntries),
            NewEntries = leaves.SelectMany(x => x.Result.NewEntries)
        };
    }

    private static void GetLeaves(WatchNode current, List<WatchNode> result) {
        if (current.Children.Count == 0)
            result.Add(current);
        foreach (var c in current.Children)
            GetLeaves(c, result);
    }
}
