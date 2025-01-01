using System;
using System.Collections.Generic;
using Quine.HRCatalog;
using Quine.Schemas.Graph;

namespace Quine.Graph;

/// <summary>
/// Provides a way to access the state defined by schema classes.
/// </summary>
public abstract class GraphSchemaHook : ITreeIdentity
{
    private readonly List<ITreeIdentity> children = new();

    /// <summary>
    /// State from the data model.
    /// </summary>
    public GraphRuntimeHook State { get; }

    /// <inheritdoc/>
    public int Id => State.Id;

    /// <inheritdoc/>
    public ITreeIdentity Owner => ((GraphRuntimeHook)State.Owner).RuntimeObject;

    /// <inheritdoc/>
    public TreePathId PathId => State.PathId;

    private protected GraphSchemaHook(ITreeIdentity owner, GraphRuntimeHook state) {
        State = QHEnsure.NotNull(state);
        State.SetRuntimeObject(this);
        if (owner is GraphSchemaHook hook) {
            QHEnsure.State(!hook.children.Contains(this));    // Enforce uniqueness.
            hook.children.Add(this);
        }
    }
}

/// <summary>
/// Generic version of <see cref="GraphSchemaHook"/>.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class GraphSchemaHook<T> : GraphSchemaHook where T : Schemas.Graph.GraphRuntimeHook
{
    /// <summary>
    /// Strongly-typed state.  This is NOT an override, but hiding.
    /// </summary>
    public new T State => (T)base.State;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="owner">The owning entity.</param>
    /// <param name="state">State instance.</param>
    protected GraphSchemaHook(ITreeIdentity owner, T state) : base(owner, state) { }
}
