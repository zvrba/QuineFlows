using System;
using System.Runtime.Serialization;

namespace Quine.HRCatalog;

/// <summary>
/// Notification event to be presented to the user.
/// </summary>
[DataContract(Namespace = Schemas.Core.XmlNamespaces.Core_1_0)]
public class QHNotificationEvent : Schemas.Core.Eventing.OperationalEvent
{
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="hMessage">Structured message to log.</param>
    /// <param name="exn">Exception wrapped in this event.  Pass <c>null</c> when none.</param>
    /// <param name="args">Parameters to fill in the event message.</param>
    public QHNotificationEvent(QHMessage hMessage, Exception exn, params object[] args) : base(hMessage.HResult.ToEventId()) {
        Message = hMessage.Format(args);
        if ((Exception = exn) != null)
            Data = Schemas.Core.Eventing.ExceptionPropertyBag.Create(exn);
    }

    /// <summary>
    /// Optional: exception information.  Setting it to non-null populates <see cref="ExceptionRecords"/>, but
    /// does not override the severity initially set by the constructor.
    /// May be <c>null</c>.
    /// </summary>
    public Exception Exception { get; }
}
