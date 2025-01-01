using System;

namespace Quine.FileWatcher;

// TODO! Move error codes to HRCatalog!!!

/// <summary>
/// Classifies the error instance reported by <see cref="ErrorInfo"/>.
/// </summary>
public enum ErrorCode
{
    /// <summary>
    /// An error unclassifiable by this enum.  See exception.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Could not fetch or refresh the size of the file.
    /// </summary>
    SizeRefreshFailed,

    /// <summary>
    /// Could not enumerate files in the directory.
    /// </summary>
    DirectoryEnumerationFailed,

    /// <summary>
    /// The name of the filesystem entry contains illegal characters.
    /// </summary>
    IllegalName
}

/// <summary>
/// Describes errors encountered during file/directory operations.
/// </summary>
public readonly struct ErrorInfo
{
    /// <summary>
    /// Set by ctor.
    /// </summary>
    public readonly object Sender;

    /// <summary>
    /// Set by ctor.
    /// </summary>
    public readonly ErrorCode ErrorCode;

    /// <summary>
    /// Set by ctor.
    /// </summary>
    public readonly string Path;

    /// <summary>
    /// Set by ctor.
    /// </summary>
    public readonly Exception Exception;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="sender">The reporting object.</param>
    /// <param name="errorCode">Code describing the error.</param>
    /// <param name="path">Path that caused the error.</param>
    /// <param name="exception">Exception that occurred.</param>
    public ErrorInfo(object sender, ErrorCode errorCode, string path, Exception exception) {
        Sender = sender;
        ErrorCode = errorCode;
        Path = path;
        Exception = exception;
    }

    /// <summary>
    /// Defines equality of <see cref="ErrorInfo"/> instances as equlity of their <see cref="Path"/> members.
    /// This is a stateless class and cannot be instantiated directly; use <see cref="Instance"/>.
    /// </summary>
    public class PathEquality : System.Collections.Generic.IEqualityComparer<ErrorInfo>
    {
        /// <summary>
        /// The (only) instance of this class.
        /// </summary>
        public static readonly PathEquality Instance = new();

        private PathEquality() { }

        /// <inheritdoc/>
        public bool Equals(ErrorInfo x, ErrorInfo y) => x.Path.Equals(y.Path);

        /// <inheritdoc/>
        public int GetHashCode([System.Diagnostics.CodeAnalysis.DisallowNull] ErrorInfo obj) => obj.Path.GetHashCode();
    }
}

