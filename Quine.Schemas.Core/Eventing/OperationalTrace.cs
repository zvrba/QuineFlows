using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Threading;

using Quine.HRCatalog;

namespace Quine.Schemas.Core.Eventing
{
    /// <summary>
    /// Persists an event trace generated during a single "logical operation".  The trace can be serialized
    /// (i.e., <see cref="Events"/> property accessed) only after the instance has been disposed, after which
    /// no further events can be added.  <see cref="EventsSnapshot"/> can be used to inspect history on an
    /// active instance.
    /// </summary>
    [DataContract(Namespace = XmlNamespaces.Core_1_0)]
    public sealed class OperationalTrace : IDisposable
    {
        // Also used as lock.  Assigned in OnDeserialized()
        private List<StackTrace> freeze = new List<StackTrace>(2);

        [OnDeserializing]
        private void OnDeserializingCB(StreamingContext _) {
            freeze = new List<StackTrace>{ new StackTrace(true) };
            events = new();
        }

        /// <summary>
        /// True if the instance has been disposed: no further modifications are allowed, and it is safe to access
        /// data members.  Safe for multi-threaded access.
        /// </summary>
        public bool IsDisposed {
            get {
                lock (freeze)
                    return freeze.Count > 0;
            }
        }

        /// <summary>
        /// True if there are no errors or messages.  Safe for multi-thread access, but <c>false</c> result may be stale.
        /// </summary>
        public bool IsEmpty {
            get {
                lock (freeze)
                    return events.Count == 0;
            }
        }

        /// <summary>
        /// True if any errors or exceptions have been recorded.  Safe for multi-thread access, but <c>false</c> result may be stale.
        /// </summary>
        /// <remarks>
        /// An event is an "error" event if <see cref="EventSeverity"/> flags are used and the flag value is error.
        /// An event is an "exception" if <see cref="OperationalEvent.Data"/> contains <see cref="ExceptionPropertyBag"/>
        /// object.
        /// </remarks>
        public bool HasErrors {
            get {
                lock (freeze)
                    return events.Any(x => ((x.EventId.Value >> 30) & 3) == EventSeverity.Error || x.Data is ExceptionPropertyBag);
            }
        }

        /// <summary>
        /// True if any events with high severity have been recorded (including errors).
        /// Safe for multi-thread access, but <c>false</c> result may be stale.
        /// </summary>
        public bool HasWarningsOrErrors {
            get {
                lock (freeze)
                    return events.Any(x => x.EventId.Value < 0);
            }
        }

        /// <summary>
        /// Any added events that don't specify a source will get the value of this property as the source.  May be null.
        /// </summary>
        public string Source => source;
        [DataMember(Name = "Source")]
        private readonly string source;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="source">
        /// Source name for this trace; should uniquely identify this trace instance. Must not be null.
        /// </param>
        /// <param name="sfp">Source file path.  Leave as default (null).</param>
        /// <param name="smb">Caller member name.  Leave as default (null).</param>
        /// <seealso cref="OperationalEvent.Source"/>
        public OperationalTrace(string source) {
            this.source = QHEnsure.NotEmpty(source);
        }

        /// <summary>
        /// Retrieves the list of events.  If the instance has not been disposed (i.e., more events can arrive),
        /// the value is <c>null</c>.  Disposed instance with no events will return an empty list.
        /// </summary>
        public IReadOnlyList<OperationalEvent> Events => _Events;

        /// <summary>
        /// Retrieves the snapshot (shallow copy) of current event list.  Unlike, <see cref="Events"/>, this may
        /// be accessed even if the instance is not disposed.
        /// </summary>
        public IReadOnlyList<OperationalEvent> EventsSnapshot { 
            get {
                lock (freeze)
                    return IsDisposed ? events : events.ToArray();
            }
        }

        // Serialization safety
        [DataMember(Name = "Events")]
        private List<OperationalEvent> _Events {
            get {
                lock (freeze) {
                    return !IsDisposed ? null : events;
                }
            }
            set {   // Used only by deserialization.
                events = value;
            }
        }
        private List<OperationalEvent> events = new List<OperationalEvent>();

        /// <summary>
        /// Each invocation marks <c>this</c> as frozen and records the stack trace.
        /// </summary>
        public void Dispose() {
            lock (freeze)
                freeze.Add(new StackTrace(true));
        }

        /// <summary>
        /// Adds a record to the log.
        /// </summary>
        public void Add(OperationalEvent e) {
            if (e == null)
                throw new ArgumentNullException(nameof(e));
            
            e.Source = Source;
            lock (freeze) {
                ThrowIfDisposed();
                events.Add(e);
            }
        }

        /// <summary>
        /// Convenience method that calls <see cref="OperationalEvent.OperationalEvent(int, string, string, object, int)"/>
        /// and forwards the object to <see cref="Add(OperationalEvent)"/>.
        /// </summary>
        public void Add(
            EventId id,
            string message,
            string source = null,
            object data = null,
            int include = PropertyValueBag.IncludeProperties
        ) => Add(new OperationalEvent(id.Value, message, source, data, include));
        
        public void AddRange(IEnumerable<OperationalEvent> events) {
            lock (freeze) {
                ThrowIfDisposed();
                this.events.AddRange(events);
            }
        }

        /// <summary>
        /// Appends events to this trace.
        /// </summary>
        /// <param name="other">
        /// Operational trace from which to append events.  It must be "disposed".
        /// </param>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="ObjectDisposedException"/>
        /// <exception cref="InvalidOperationException">The source trace is not disposed.</exception>
        public void Append(OperationalTrace other) {
            if (other == null)
                throw new ArgumentNullException(nameof(other));
            lock (freeze) {
                ThrowIfDisposed();

                int firstAdded = events.Count;
                events.AddRange(other.Events);  // .Events throws IOE unless disposed.

#if false
                if (Source != null) {
                    for (int i = firstAdded; i < events.Count; ++i)
                        events[i].Source = Source;
                }
#endif
            }
        }

        private void ThrowIfDisposed() {
            Debug.Assert(Monitor.IsEntered(freeze));
            if (freeze.Count > 0)
                throw new ObjectDisposedException(nameof(OperationalTrace));
        }
    }
}
