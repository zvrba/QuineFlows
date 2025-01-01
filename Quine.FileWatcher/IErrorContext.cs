using System;

namespace Quine.FileWatcher;

/// <summary>
/// Provides an error-handling context.
/// </summary>
public interface IErrorContext
{
    /// <summary>
    /// Invoked when an error is encountered.  This method MUST NOT rethrow the exception directly as it's also
    /// invoked by exception filters; <see cref="ExceptionFilter(in ErrorInfo, out Exception)"/>.
    /// </summary>
    /// <param name="errorInfo">Describes the error.</param>
    /// <returns>
    /// Null to swallow the thrown exception.  Otherwise, the returned exception should be re-thrown by the caller.
    /// If the returned instance is not the same exception as received in <paramref name="errorInfo"/>, it should
    /// wrap the original exception.
    /// </returns>
    Exception Accept(in ErrorInfo errorInfo);

    /// <summary>
    /// Intended to be used as exception filter.  Invokes <see cref="Accept(in ErrorInfo)"/> to fill <paramref name="translated"/>.
    /// </summary>
    /// <param name="errorInfo">Describes the error.</param>
    /// <param name="translated">Set to translated exception, as returned by <see cref="Accept(in ErrorInfo)"/>.</param>
    /// <returns>
    /// True if the translated exception is not the same as the one in <paramref name="errorInfo"/>.
    /// </returns>
    bool ExceptionFilter(in ErrorInfo errorInfo, out Exception translated) {
        translated = Accept(errorInfo);
        return translated != errorInfo.Exception;
    }

    /// <summary>
    /// Default implementation.  The <see cref="Accept(in ErrorInfo)"/> method just returns the incoming exception.
    /// This class is stateless and cannot be instantiated directly; use <see cref="Instance"/> instead.
    /// </summary>
    /// <remarks>
    /// The implementation should consider ignorin <see cref="System.IO.DirectoryNotFoundException"/> unless all directories
    /// are certain to be present in advance.
    /// </remarks>
    public class Default : IErrorContext
    {
        /// <summary>
        /// The (single) instance.
        /// </summary>
        public static readonly Default Instance = new();
        private Default() { }

        /// <inheritdoc/>
        public Exception Accept(in ErrorInfo errorInfo) => errorInfo.Exception;
    }
}
