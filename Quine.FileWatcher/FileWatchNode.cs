using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Quine.HRCatalog;

namespace Quine.FileWatcher;

sealed class FileWatchNode : WatchNode
{
    readonly object value;
    readonly Dictionary<string, SimpleDirectoryMonitor> monitors = new();

    internal FileWatchNode(WatchNode parent, Schemas.Core.PathComponents pattern, object value) : base(parent, null, pattern) {
        if (Pattern.Length != 1)
            throw new ArgumentException("Invalid pattern length.");
        if (Parent == null)
            throw new ArgumentNullException(nameof(Parent), nameof(FileWatchNode) + " cannot be root.");
        this.value = value ?? throw new ArgumentNullException(nameof(value));
    }

    // FileWatchNode can have no siblings.
    private protected override bool IsEquivalent(WatchNode other) {
        if (other is not FileWatchNode wn)
            throw CreateWatchConflictException(QHNucleus.Filesystem.E_Watchfolder_Sibling_TypeConflict, other, this);
        if (wn.Pattern != Pattern)
            throw CreateWatchConflictException(QHNucleus.Filesystem.E_Watchfolder_Sibling_PatternConflict, other, this);
        if (!wn.value.Equals(value))
            throw CreateWatchConflictException(QHNucleus.Filesystem.E_Watchfolder_Sibling_ValueConflict, other, this);
        return true;
    }

    private protected override WatchNode Clone(WatchNode clonedParent, WatchNode clonedThis) {
        clonedThis = new FileWatchNode(clonedParent, Pattern, value);
        return base.Clone(clonedParent, clonedThis);
    }

    private protected override WatchNode AddChild(WatchNode node, WatchNode equivalent) =>
        throw new NotSupportedException("Leaf cannot have children.");

    private protected override void Update() {
        var allEntries = Enumerable.Empty<WatchResultEntry>();
        var newEntries = Enumerable.Empty<WatchResultEntry>();
        foreach (var e in Parent.Result.AllEntries) {
            var m = Update(e);
            if (m == null)
                continue;
            allEntries = allEntries.Concat(m.AllEntries.Select(x => Make(e, x)));
            newEntries = newEntries.Concat(m.NewEntries.Select(x => Make(e, x)));
        }
        Result = new WatchResult() {
            AllEntries = allEntries,
            NewEntries = newEntries
        };
        // Dont't call base, no children to update.

        WatchResultEntry Make(WatchResultEntry current, Schemas.Core.PathComponents p) =>
            current.With(p, Pattern.NormalizedString, value);
    }

    private SimpleDirectoryMonitor Update(WatchResultEntry current) {
        if (!monitors.TryGetValue(current.Path.NormalizedString, out var m)) {
            // We don't want to create an instance for directories that may never get created (due to parent parameters)
            if (!Directory.Exists(current.Path.NativeString))
                return null;
            // ctor throws if the directory doesn't exist.
            m = new SimpleDirectoryMonitor(current.Path) { ErrorContext = this.ErrorContext };
            monitors.Add(current.Path.NormalizedString, m);
        }
        m.Update();
        return m;
    }
}