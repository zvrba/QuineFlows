using System;
using System.Collections.Generic;

using Quine.HRCatalog;

namespace Quine.FileWatcher;

sealed class ParameterWatchNode : WatchNode
{
    readonly WatchParameterParser parser;
    readonly TraversalVisitor traversal;
    readonly HashSet<WatchResultEntry> allEntries;
    readonly HashSet<WatchResultEntry> newEntries;

    internal ParameterWatchNode(WatchNode parent, Schemas.Core.PathComponents pattern, WatchParameterParser parser) : base(parent, null, pattern) {
        if (Pattern.Length != 1)
            throw new ArgumentException("Invalid pattern length.");
        if (Parent == null)
            throw new ArgumentNullException(nameof(parent), nameof(ParameterWatchNode) + " cannot be root.");
        for (WatchNode p = Parent; p != null; p = p.Parent) {
            if (p is ParameterWatchNode pn && pn.Pattern == Pattern)
                throw CreateWatchConflictException(QHNucleus.Filesystem.E_Watchfolder_Path_ParameterConflict, p, this);
        }

        this.parser = parser ?? throw new ArgumentNullException(nameof(parser));
        this.traversal = new TraversalVisitor() {
            Filter = this.Filter,
            ErrorContext = this.ErrorContext
        };
        this.allEntries = new(WatchResultEntry.PathComparer.Instance);
        this.newEntries = new(WatchResultEntry.PathComparer.Instance);
        this.Result = new WatchResult() {
            AllEntries = allEntries,
            NewEntries = newEntries
        };
    }

    private TraversalFilterInstruction Filter(Schemas.Core.PathComponents p) =>
        parser(p[-1], out var _) ? TraversalFilterInstruction.Accept : TraversalFilterInstruction.Discard;

    // ParameterWatchNode can't have other siblings.
    private protected override bool IsEquivalent(WatchNode other) {
        if (other is ParameterWatchNode wn && wn.Pattern == Pattern)
            return true;
        throw CreateWatchConflictException(QHNucleus.Filesystem.E_Watchfolder_SiblingConflict, other, this);
    }

    private protected override WatchNode Clone(WatchNode clonedParent, WatchNode clonedThis) {
        clonedThis = new ParameterWatchNode(clonedParent, Pattern, parser);
        return base.Clone(clonedParent, clonedThis);
    }

    private protected override void Update() {
        newEntries.Clear();
        foreach (var e in Parent.Result.AllEntries)
            Update(e);
        base.Update();
    }

    private void Update(WatchResultEntry current) {
        foreach (var e in traversal.GetDirectories(current.Path)) {
            if (!parser(e[-1], out var v)) {
                var exn = ErrorContext.Accept(new ErrorInfo(this, ErrorCode.IllegalName, e.NormalizedString, new NotImplementedException($"BUG: parser returned inconsistend status for name {e[-1]}.")));
                if (exn != null)
                    throw exn;
            }
            var r = current.With(e, Pattern[0], v);
            if (allEntries.Add(r))
                newEntries.Add(r);
        }
    }
}
