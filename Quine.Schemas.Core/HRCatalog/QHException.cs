using System;

namespace Quine.HRCatalog;

/// <summary>
/// Marker interface for exceptions that can be classified through HRCatalog.  The implementing exception
/// MUST set <c>HResult</c> property and it SHOULD override <c>Message</c> property.  Only classes deriving
/// from <c>Exception</c> can implement this interface, otherwise <see cref="InvalidCastException"/> will
/// be thrown when methods are attempted to be used.
/// </summary>
public interface IQHException
{
    private Exception AsException => ((Exception)this);

    QHResult HResult => QHResult.FromHResult(AsException.HResult);

    bool IsInformation => HResult.IsInformation;
    bool IsWarning => HResult.IsWarning;
    bool IsError => HResult.IsError;
    bool IsCritical => HResult.IsCritical;
    bool IsStunned => QHBugs.IsStunned(AsException);
}

/// <summary>
/// Default exception type that should be thrown when a message is available in HRCatalog.
/// </summary>
public class QHException : Exception, IQHException
{
    public QHException(QHMessage hMessage, Exception inner = null, params object[] args) : base(hMessage.Format(args), inner) {
        HResult = hMessage.HResult;
    }
}
