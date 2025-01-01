using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Quine.FileWatcher;

/// <summary>
/// Collection of watch results.
/// </summary>
public readonly struct WatchResult
{
    /// <summary>
    /// All entries ever observed.  WARNING: Lazily evaluated.
    /// </summary>
    public IEnumerable<WatchResultEntry> AllEntries { get; internal init; }

    /// <summary>
    /// New entries onserved only in the last round.  WARNING: Lazily evaluated.
    /// </summary>
    public IEnumerable<WatchResultEntry> NewEntries { get; internal init; }
}

/// <summary>
/// Single entry reported by walking the watch tree.
/// </summary>
/// <seealso cref="RootWatchNode.Walk"/>.
public readonly struct WatchResultEntry
{
    /// <summary>
    /// Path that this entry pertains to.
    /// </summary>
    public readonly Schemas.Core.PathComponents Path;

    /// <summary>
    /// Parametr values parsed out from the path.  This is never null.
    /// </summary>
    public readonly ImmutableDictionary<string, object> Parameters;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="path">Value for <see cref="Path"/>.</param>
    /// <param name="parameters">Value for <see cref="Parameters"/>.  If null, an empty dictionary is substituted.</param>
    public WatchResultEntry(Schemas.Core.PathComponents path, ImmutableDictionary<string, object> parameters = null) {
        Path = path;
        Parameters = parameters ?? ImmutableDictionary.Create<string, object>();
    }

    internal WatchResultEntry With(Schemas.Core.PathComponents path, string parameter, object value) {
        if (string.IsNullOrWhiteSpace(parameter))
            throw new ArgumentNullException(nameof(parameter));
        if (value == null)
            throw new ArgumentNullException(nameof(parameter));
        return new WatchResultEntry(path, Parameters.Add(parameter, value));
    }

    /// <summary>
    /// Implements comparison and equality over <see cref="Path"/>.  The comparison used is case-sensitive ordinal.
    /// This class is stateless and cannot be instantiated directly; use <see cref="Instance"/> instead.
    /// </summary>
    public class PathComparer : IEqualityComparer<WatchResultEntry>, IComparer<WatchResultEntry>
    {
        /// <summary>
        /// The (single) instance.
        /// </summary>
        public static readonly PathComparer Instance = new();
        private PathComparer() { }

        /// <inheritdoc/>
        public int Compare(WatchResultEntry x, WatchResultEntry y) =>
            StringComparer.Ordinal.Compare(x.Path.NormalizedString, y.Path.NormalizedString);

        /// <inheritdoc/>
        public bool Equals(WatchResultEntry x, WatchResultEntry y) =>
            StringComparer.Ordinal.Equals(x.Path.NormalizedString, y.Path.NormalizedString);

        /// <inheritdoc/>
        public int GetHashCode(WatchResultEntry obj) => obj.Path.NormalizedString.GetHashCode();
    }
}
