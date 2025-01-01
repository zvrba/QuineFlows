using System;
using System.Linq;
using Quine.HRCatalog;

namespace Quine.FileWatcher;

sealed class ConstantWatchNode : WatchNode
{
    internal ConstantWatchNode(WatchNode parent, Schemas.Core.PathComponents pattern) : base(parent, null, pattern) {
        if (Pattern.Length != 1)
            throw new ArgumentException("Invalid pattern length.");
        if (Parent == null)
            throw new ArgumentNullException(nameof(parent), nameof(ConstantWatchNode) + " cannot be root.");
    }

    // Constant node can have only other constant nodes as siblings.
    private protected override bool IsEquivalent(WatchNode other) {
        if (other is not ConstantWatchNode wn)
            throw CreateWatchConflictException(QHNucleus.Filesystem.E_Watchfolder_SiblingConflict, other, this);
        return wn.Pattern == Pattern;
    }

    private protected override WatchNode Clone(WatchNode clonedParent, WatchNode clonedThis) {
        clonedThis = new ConstantWatchNode(clonedParent, Pattern);
        return base.Clone(clonedParent, clonedThis);
    }

    private protected override void Update() {
        Result = new WatchResult() {
            AllEntries = Parent.Result.AllEntries.Select(Make),
            NewEntries = Parent.Result.NewEntries.Select(Make)
        };
        base.Update();

        WatchResultEntry Make(WatchResultEntry current) => new WatchResultEntry(
            Schemas.Core.PathComponents.Join(current.Path, Pattern),
            current.Parameters);
    }
}

