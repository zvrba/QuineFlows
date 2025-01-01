using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Quine.HRCatalog;

namespace Quine.FileWatcher;

/// <summary>
/// Delegate type for parsing parameter nodes into values.  Implementations MUST NOT throw.
/// </summary>
/// <param name="input">String to parse.</param>
/// <param name="value">Set to the parsed value on succes, or null on failure.</param>
/// <returns>
/// True if parsing succeeded.
/// </returns>
public delegate bool WatchParameterParser(string input, out object value);

/// <summary>
/// A node in the "watch tree" which watches directory hierarchy for stable files.
/// </summary>
public abstract class WatchNode
{
    private ImmutableArray<WatchNode> children = ImmutableArray.Create<WatchNode>();
    
    /// <summary>
    /// Error-reporting context.
    /// </summary>
    protected IErrorContext ErrorContext { get; }

    /// <summary>
    /// Pattern that this node matches.  Parameter names have parameter name as pattern (excluding the delimiters) and leaf
    /// nodes have <c>!</c> as pattern.  This property must not be parsed, the node's concrete type must be checked instead.
    /// </summary>
    public Schemas.Core.PathComponents Pattern { get; }

    /// <summary>
    /// Parent of this node.  Null for the root node.
    /// </summary>
    public WatchNode Parent { get; }

    /// <summary>
    /// Children of this node.  Never null, but may be empty.
    /// </summary>
    public IReadOnlyList<WatchNode> Children => children;

    /// <summary>
    /// Set by (overridden) <see cref="Update"/> to the result of this node.
    /// </summary>
    internal WatchResult Result { get; private protected set; }

    private protected WatchNode(WatchNode parent, IErrorContext errorContext, Schemas.Core.PathComponents pattern) {
        Parent = parent;
        Pattern = pattern;
        ErrorContext = errorContext ?? Parent.ErrorContext;
    }

    /// <summary>
    /// Creates the root of the watch tree without a root path or error context.  The tree must be cloned
    /// (<see cref="Clone(WatchNode, Schemas.Core.PathComponents, IErrorContext)"/>) to be actually usable
    /// for walking the file-system (<see cref="Walk(WatchNode)"/>).
    /// </summary>
    public static WatchNode MakeRoot() => new RootWatchNode();

    /// <summary>
    /// Replaces root path and error context in the tree rooted at <paramref name="root"/> and returns a new tree.
    /// </summary>
    /// <param name="root">Root of the tree.  Must be a value returned by <see cref="MakeRoot"/>.</param>
    /// <param name="rootPath">Absolute path to the directory to watch.</param>
    /// <param name="errorContext">Error context to use.  Must not be null.</param>
    /// <returns>
    /// A deeply cloned tree instance with parameters replaced.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// If any preconditions on the arguments are not satisfied.
    /// </exception>
    public static WatchNode Clone(WatchNode root, Schemas.Core.PathComponents rootPath, IErrorContext errorContext) {
        if (root is not RootWatchNode)
            throw new ArgumentOutOfRangeException(nameof(root), "Invalid node type.");
        var newRoot = new RootWatchNode(rootPath, errorContext);
        return root.Clone(null, newRoot);
    }

    /// <summary>
    /// Walks the watch tree and reports results.
    /// </summary>
    /// <param name="root">Must point to a configured root as returned by <see cref="Clone(WatchNode, Schemas.Core.PathComponents, IErrorContext)"/>.</param>
    /// <returns>
    /// An instance of <see cref="WatchResult"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="root"/> does not point to a root node.</exception>
    public static WatchResult Walk(WatchNode root) {
        if (root is not RootWatchNode rwn)
            throw new ArgumentOutOfRangeException(nameof(root), "Invalid node type.");
        return rwn.Walk();
    }

    /// <summary>
    /// Makes a child node or returns an existing one matching <paramref name="pattern"/>.
    /// </summary>
    /// <param name="pattern">
    /// Pattern to parse.  This is also the key for the parameter value of parameter and file nodes.
    /// Parameters must be surrounded with <c>$()</c>.
    /// </param>
    /// <param name="parser">
    /// Must not be null iff a parameter node is being created.
    /// </param>
    /// <param name="value">
    /// Must not be null iff a file (leaf) node is being created.  NB! This object is NOT cloned during
    /// <see cref="Clone(WatchNode, Schemas.Core.PathComponents, IErrorContext)"/>.
    /// </param>
    /// <exception cref="WatchConflictException">When a conflicting node is attempted added.</exception>
    /// <remarks>
    /// This is a somewhat unfortunate API because various parameter combinations are used to create all 3 kinds of watch nodes.
    /// <list type="table">
    /// <item>
    /// <term>Constant node</term>
    /// <description>
    /// Created when <paramref name="pattern"/> is a string NOT enclosed in <c>$()</c>.  In this case,
    /// <paramref name="parser"/> and <paramref name="value"/> must both be <c>null</c>.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Parameter node</term>
    /// <description>
    /// Created when <paramref name="pattern"/> is a string enclosed in <c>$()</c> (a "parameter"), for example <c>$(DayNumber)</c>.
    /// In this case, <paramref name="parser"/>  MUST be provided, and <paramref name="value"/> MUST be <c>null</c>.
    /// The dictionary in <see cref="WatchResultEntry.Parameters"/> will have the parameter name (without enclosing <c>$()</c>)
    /// as the key and the object returned by <paramref name="parser"/> as value.
    /// </description>
    /// </item>
    /// <item>
    /// <term>Leaf node</term>
    /// <description>
    /// Created when <paramref name="pattern"/> is a parameter and <paramref name="value"/> is NOT <c>null</c>.
    /// As above, the dictionary in <see cref="WatchResultEntry.Parameters"/> will have the parameter name as the key
    /// and provided <paramref name="value"/> as the value.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    public WatchNode MakeChild(
        Schemas.Core.PathComponents pattern,
        WatchParameterParser parser = null,
        object value = null) {
        WatchNode ret = null;
        if (IsParameter(pattern)) {
            if (parser == null && value == null)
                throw new ArgumentException("Parser or device must be provided for parameters.");
            if (value == null) ret = new ParameterWatchNode(this, pattern, parser);
            else ret = new FileWatchNode(this, pattern, value);
        } else {
            if (parser != null || value != null)
                throw new ArgumentException("Constant node cannot specify a parser or value.");
            ret = new ConstantWatchNode(this, pattern);
        }

        WatchNode equivalent = null;
        foreach (var c in Children) {   // NB! Does not break early, all must be checked for conflicts.
            if (ret.IsEquivalent(c)) {
                if (equivalent != null)
                    throw new NotImplementedException("BUG: multiple equivalent nodes found.");
                equivalent = c;
            }
        }
        return AddChild(ret, equivalent);

        static bool IsParameter(Schemas.Core.PathComponents pattern) =>
            pattern[0].StartsWith("$(") && pattern[0].EndsWith(")");
    }

    /// <summary>
    /// Checks whether <c>this</c> is equivalent to <paramref name="other"/>.
    /// This is invoked for all siblings of the node to be added.
    /// </summary>
    /// <param name="other">Other instance to check equivalence with.  Will never be null.</param>
    /// <exception cref="WatchConflictException">When this conflicts with other sibling.</exception>
    private protected abstract bool IsEquivalent(WatchNode other);

    /// <summary>
    /// Clones children of <c>this</c>.
    /// </summary>
    /// <param name="clonedParent">
    /// The cloned parent node.
    /// </param>
    /// <param name="clonedThis">
    /// On entry this is null.  Override must clone <c>this</c> and invoke base with the cloned instance.
    /// </param>
    private protected virtual WatchNode Clone(WatchNode clonedParent, WatchNode clonedThis) {
        if (clonedThis == this || clonedThis.GetType() != GetType())
            throw new NotImplementedException("BUG: derived clone did not return a new instance of correct type.");
        clonedThis.children = this.children.Select(x => x.Clone(clonedThis, null)).ToImmutableArray();
        if (clonedThis.children.Any(x => x.Parent != clonedThis))
            throw new NotImplementedException("BUG: cloning did not preserve parent relation.");
        return clonedThis;
    }

    /// <summary>
    /// Updates <see cref="Result"/>. MUST be overridden; see remarks.
    /// </summary>
    /// <remarks>
    /// This implementation only recursively calls <see cref="Update"/> on all <see cref="Children"/>.  Derived class
    /// must override this to first set <see cref="Result"/> then delegate to the base implementation.
    /// </remarks>
    private protected virtual void Update() {
        foreach (var c in Children)
            c.Update();
    }

    
    /// <summary>
    /// Adds <paramref name="node"/> to the children list if <paramref name="equivalent"/> is null.
    /// </summary>
    /// <returns><paramref name="equivalent"/> if not null, otherwise <paramref name="node"/>.</returns>
    /// <remarks>
    /// Derived classes may override this to perform other checks.  The derived implementation must defer to
    /// base if the child is actually to be added.
    /// </remarks>
    private protected virtual WatchNode AddChild(WatchNode node, WatchNode equivalent) {
        if (equivalent != null) node = equivalent;
        else children = children.Add(node);
        return node;
    }

    /// <summary>
    /// Creates an instance of <see cref="WatchConflictException"/> with conflict details filled in.
    /// </summary>
    /// <param name="hMessage">Exception message.</param>
    /// <param name="existing">Existing watch node.</param>
    /// <param name="conflicting">Conflicting watch node.</param>
    /// <returns>The exception instance.</returns>
    protected static WatchConflictException CreateWatchConflictException(QHMessage hMessage, WatchNode existing, WatchNode conflicting) {
        var conflictingPath = GetPath(conflicting);
        var existingPath = GetPath(existing);
        return new WatchConflictException(
            hMessage,
            conflictingPath.NormalizedString,
            existing.Children.Count == 0 ?
                new string[] { existingPath.NormalizedString } :
                existing.Children.Select(x => Schemas.Core.PathComponents.Join(existingPath, x.Pattern).NormalizedString).ToArray());

        static Schemas.Core.PathComponents GetPath(WatchNode node) {
            var parts = new List<Schemas.Core.PathComponents>(8);
            for (; node is not RootWatchNode; node = node.Parent)
                parts.Add(node.Pattern);
            parts.Reverse();
            return parts.Count == 0 ?
                Schemas.Core.PathComponents.Make("(root)") :
                Schemas.Core.PathComponents.Join(parts.ToArray());
        }
    }
}
