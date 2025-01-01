using System;
using System.Threading.Tasks;

namespace Quine.Schemas.Core.Repository;

/// <summary>
/// Provides operations related to the database provider.
/// </summary>
public interface IQdbSource
{
    public static class Providers {
        public const string VistaDb = nameof(VistaDb);
        public const string AzSqlDb = nameof(AzSqlDb);
    }

    /// <summary>
    /// Possible return values from <see cref="CheckSchemaVersionAsync(int, int)"/>.
    /// </summary>
    public enum SchemaVersionCheckResult {
        /// <summary>
        /// The database version and compiled-in version match.  The databse is directly usable.
        /// </summary>
        Compatible,
        
        /// <summary>
        /// The database version is older than the compiled-in version, but upgrade is possible.
        /// </summary>
        UpgradePossible,

        /// <summary>
        /// Incompatible database: either newer than the compiled-in version, or major version mismatch with the compiled-in version.
        /// </summary>
        Incompatible,

        /// <summary>
        /// No schema version found; db does not belong to us.
        /// </summary>
        ForeignDatabase,
    }

    /// <summary>
    /// One of constants defined in <see cref="Providers"/>.
    /// Used to code provider-specific queries and exception handling.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Schema in which all tables reside.  May be null.
    /// </summary>
    string SchemaName { get; }

    /// <summary>
    /// Provides change and trace events.  Events are shared across all connection instances.
    /// </summary>
    /// <seealso cref="QdbEvents"/>
    QdbEvents Events { get; }

    /// <summary>
    /// Checks the compiled-in schema version against the database's schema version.
    /// </summary>
    /// <param name="compiledMajorNumber">The compiled-in major version number.</param>
    /// <param name="compiledOrdinalNumber">The compiled-in ordinal version number.</param>
    /// <returns>
    /// One of <see cref="SchemaVersionCheckResult"/> values.
    /// </returns>
    Task<SchemaVersionCheckResult> CheckSchemaVersionAsync(int compiledMajorNumber, int compiledOrdinalNumber);

    /// <summary>
    /// Opens a connection to the database.
    /// </summary>
    /// <returns>
    /// An instance of <see cref="IQdbConnection"/>.
    /// </returns>
    Task<IQdbConnection> OpenConnectionAsync();

    /// <summary>
    /// True if the DB supports bidirectional streaming of LOBs (VAR{CHAR | BINARY}(MAX), XML) instead of staging them in-memory.
    /// Default implementation returns false.
    /// </summary>
    bool SupportsLargeObjectStreaming => false;

    /// <summary>
    /// Returns a schema-qualified name for <typeparamref name="TEntity"/>.
    /// </summary>
    /// <seealso cref="SchemaName"/>
    string GetEntityDbName<TEntity>() where TEntity : IQdbEntity<TEntity> =>
        string.IsNullOrEmpty(SchemaName) ? TEntity.EntityDbName : $"{SchemaName}.{TEntity.EntityDbName}";

    static void GetDefaultAccessor<TEntity>(ref EntityAccessor<TEntity> accessor)
        where TEntity : IQdbEntity<TEntity>
    {
        if (accessor.IsDefault)
            accessor = TEntity.EntityAccessor;
    }

}

