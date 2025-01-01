using System;
using System.Runtime.Serialization;

namespace Quine.Schemas.Core.Eventing
{
    /// <summary>
    /// Suggested predefined constants for using <see cref="EventId"/> flags.  The definition of flags makes error events
    /// have the lowest integer values in the negative range.  The definition leaves 6 bits for free use.
    /// </summary>
    public static class EventSeverity
    {
        public const byte Low = 0x00;
        public const byte Normal = 0x40;
        public const byte High = 0xC0;
        public const byte Error = 0x80;

        private static readonly byte[] Order = { Low >> 6, Normal >> 6, High >> 6, Error >> 6 };

        /// <summary>
        /// Comparison method for <see cref="EventId.Severity"/> values.
        /// </summary>
        /// <returns>Standard comparison result.</returns>
        public static int Compare(byte x, byte y) {
            var bx = Array.IndexOf(Order, x);
            var by = Array.IndexOf(Order, y);
            return bx - by;
        }
    }

    /// <summary>
    /// Strongly-typed wrapper for integer-valued event ids.  The integer value is accessible through <see cref="Value"/> member.
    /// </summary>
    /// <remarks>
    /// This is not a data contract struct for compactness of serialized representation.  The serializtion needs to be
    /// handled solely by <see cref="OperationalEvent"/>.
    /// </remarks>
    public readonly struct EventId : IEquatable<EventId>
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="flags">Flags for the event.  <see cref="EventSeverity"/> are the only predefined flag.</param>
        /// <param name="code">Event code.  Only lowest 24 bits may be used.</param>
        /// <exception cref="ArgumentOutOfRangeException">Code is negative or larger than ca 16M (24 bits).</exception>
        public EventId(byte flags, int code) {
            if (code < 0 || code >= (1 << 24))
                throw new ArgumentOutOfRangeException(nameof(code));
            Value = (((int)flags) << 24) | code;
        }

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        internal EventId(int value) => Value = value;

        /// <summary>
        /// Encoded value of the event id.
        /// </summary>
        public readonly int Value;

        public int Code => Value & 0xFFFFFF;
        public byte Severity => (byte)((uint)Value >> 30);
        public byte Flags => (byte)(Value >> 24);
        
        public bool IsLow => Severity == 0;
        public bool IsNormal => Severity == 1;
        public bool IsHigh => Severity == 2;
        public bool IsError => Severity == 3;
        public bool IsHighOrError => Value < 0;

        public bool Equals(EventId other) => Value == other.Value;
        public override int GetHashCode() => Value * 0x3A6FB51D;
        public override bool Equals(object obj) => obj is EventId other && Equals(other);
        public static bool operator ==(EventId e1, EventId e2) => e1.Equals(e2);
        public static bool operator !=(EventId e1, EventId e2) => !e1.Equals(e2);
    }

    /// <summary>
    /// A single event persisted by <see cref="OperationalTrace"/>.  NOT thread-safe.
    /// </summary>
    [DataContract(Namespace = XmlNamespaces.Core_1_0)]  // TODO: KnownTypes!!!
    public class OperationalEvent
    {
        /// <summary>
        /// Numeric id identifying the event.
        /// </summary>
        public EventId EventId {
            get => new(id);
            private set => id = value.Value;
        }
        [DataMember(Name = "EventId")]
        private int id;

        /// <summary>
        /// Component that generated the event.  By convention, it should be a fully-qualified name of the class type
        /// generating the event.  If not set on construction, it will be set by <see cref="OperationalTrace.Source"/>
        /// upon adding to the trace.
        /// </summary>
        /// <remarks>
        /// Setter is internal protected.  It does not overwrite an existing value.  This property exists alos on
        /// the event because traces from multiple sources can be concatenated into a single trace.
        /// </remarks>
        public string Source {
            get => source;
            internal protected set {
                if (source == null)
                    source = value;
            }
        }
        [DataMember(Name = "Source")]
        private string source;

        /// <summary>
        /// Event message.
        /// </summary>
        [DataMember(Name = "Message")]
        public string Message { get; protected set; }

        /// <summary>
        /// Additional data attached to the message.
        /// </summary>
        /// <remarks>
        /// The getter never returns null: if necessary, a default instance of <see cref="ObjectPropertyBag"/> is constructed.
        /// To determine whether this property is non-null without constructing it, use <see cref="HasData"/>.
        /// </remarks>
        public PropertyValueBag Data {
            get {
                if (data == null)
                    data = new ObjectPropertyBag();
                return data;
            }
            set { data = value; }
        }
        [DataMember(Name = "Data")]
        private PropertyValueBag data;

        /// <summary>
        /// True if <see cref="Data"/> dictionary has been constructed.
        /// </summary>
        public bool HasData => data != null;

        /// <summary>
        /// Timestamp for the message.  Defaults to <c>Now</c>.
        /// </summary>
        [DataMember]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="id">Number identifying this event.  Use <see cref="Core.EventId"/> to construct a structured value.</param>
        /// <param name="message">Message for the event.  May be null.</param>
        /// <param name="source">The <see cref="Source"/> generating the event.</param>
        /// <param name="data">
        /// Object to populate <see cref="Data"/> from.  If this is an exception, <see cref="ExceptionPropertyBag"/> will be
        /// constructed, otherwise <see cref="ObjectPropertyBag"/> will be constructed with <c>null</c> type name.
        /// If this is an instance of <see cref="PropertyValueBag"/>, it will be used verbatim.
        /// </param>
        /// <param name="include">
        /// Relevant only when <paramref name="data"/> is not an exception; determines what to include in <see cref="ObjectPropertyBag"/>.
        /// Default is only properties.
        /// </param>
        public OperationalEvent(
            int id,
            string message,
            string source = null,
            object data = null,
            int include = PropertyValueBag.IncludeProperties) : this(new(id))
        {
            this.source = source;
            this.Message = message;

            if (data is PropertyValueBag pvb) Data = pvb;
            if (data is Exception exn) Data = ExceptionPropertyBag.Create(exn);
            else if (data != null) Data = new ObjectPropertyBag(data, include);
        }

        protected OperationalEvent(EventId id) {
            this.EventId = id;
            this.id = id.Value;
            this.Timestamp = DateTime.Now;
        }

        [OnDeserialized]
        private void OnDeserializedCB(StreamingContext _) {
            EventId = new(id);
        }
    }
}
