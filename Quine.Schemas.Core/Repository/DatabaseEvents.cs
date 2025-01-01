using System;

using Quine.HRCatalog;

namespace Quine.Schemas.Core.Repository;

/// <summary>
/// Describes actions that <see cref="IKeyedRepository{K, V}"/> performs on the cached values.
/// </summary>
public enum QdbChangeType
{
    /// <summary>
    /// No action was performed.
    /// </summary>
    None,

    /// <summary>
    /// The value or entity collection was re-fetched from the backing store.
    /// </summary>
    Refresh,

    /// <summary>
    /// The value was created.
    /// </summary>
    Create,

    /// <summary>
    /// The value was replaced with another value having the same key.
    /// </summary>
    Replace,

    /// <summary>
    /// The value was deleted.
    /// </summary>
    Delete,
}

/// <summary>
/// Delegate type used to deliver change notification events to the repository.
/// </summary>
/// <param name="sender">Repository sending the notification.  It MUST NOT be accessed from the delegate's implementation.</param>
/// <param name="changeType">
/// One of <see cref="QdbChangeType"/> values denoting the type of change being performed.
/// </param>
/// <param name="value">
/// The affected value, as it is present in the repository at the time the event was fired.
/// The backing collection for <see cref="QdbChangeType.Refresh"/> when the whole cache has been refreshed.
/// </param>
public delegate void QdbChangeNotification(object sender, QdbChangeType changeType, object value);

/// <summary>
/// Events are shared by all instances of <see cref="IQdbSource"/>.
/// </summary>
public sealed class QdbEvents
{
    /// <summary>
    /// Change events are usually sent by <see cref="IDatabaseTable{TEntity}"/>, but other senders may be possible.
    /// </summary>
    public event QdbChangeNotification ChangeNotification;

    /// <summary>
    /// Emits diagnostic messages from the database.
    /// </summary>
    public event Action<object, Eventing.OperationalEvent> ShellTrace;

    public void RaiseChangeNotification
        (
        object sender,
        QdbChangeType changeType,
        object value
        )
    {
        var il = ChangeNotification?.GetInvocationList();
        if (il != null && changeType != QdbChangeType.None) {
            foreach (QdbChangeNotification d in il) {
                try {
                    d?.Invoke(sender, changeType, value);
                }
                catch (Exception e) {
                    try {
                        RaiseShellTrace(sender, new QHNotificationEvent(QHBugs.W_Balked, e, $"ChangeNotification handler from {sender.GetType().FullName} failed."));
                    }
                    catch (Exception e1) {
                        Console.WriteLine($"Event handler threw.\n{e1}");
                    }
                }
            }
        }
    }

    public void RaiseShellTrace
        (
        object sender,
        Eventing.OperationalEvent @event
        )
    {
        var il = ShellTrace?.GetInvocationList();
        if (il != null) {
            foreach (Action<object, Eventing.OperationalEvent> d in il) {
                try {
                    d?.Invoke(sender, @event);
                }
                catch (Exception e) {
                    Console.WriteLine($"Event handler threw.\n{e}");
                }
            }
        }
    }

}
