using System;
using Quine.Schemas.Core.Eventing;

/// <summary>
/// This namespace contains error code definitions for all subsystems.
/// Every error code should correspond to a custom exception with that <c>HRESULT</c> value and message
/// that is appropriate to show to the user.  Facility 0 is reserved for "general" values.
/// </summary>
/// <remarks>
/// <para>
/// Guidelines about using severity levels:
/// <list type="bullet">
/// <item>
/// Information: A rare condition has occurred, which may need handling some time in the future.
/// </item>
/// <item>
/// Warning: Error condition(s) occurred and were handled, but the error(s) may cause later failures.
/// </item>
/// <item>
/// Error: Operation was aborted due to an unhandleable error.  Must be signalled by an exception.
/// Subsequent operations may succeed.
/// </item>
/// <item>
/// Critical: The application / worker exited due to fatal error.  The application / worker won't process
/// anything further.
/// </item>
/// </list>
/// Every message definition should also be prefixed with the severity level, f.ex., <c>W_CleanupFailed</c> for
/// a warning.
/// </para>
/// </remarks>
namespace Quine.HRCatalog
{
    /// <summary>
    /// Event severity. 
    /// </summary>
    internal enum QHSeverity : uint
    {
        Information = 0x20000000,
        Warning     = 0x28000000,
        Error       = 0xA0000000,
        Critical    = 0xA8000000,
    }

    /// <summary>
    /// Builds a <c>HRESULT</c> value that is encoded as customer-defined.  The value may be extracted
    /// only by implicit conversion to <c>int</c>.  Also overrides <c>ToString</c> to display a hex-formatted
    /// error code with prepended severity letter.
    /// </summary>
    /// <remarks>
    /// This struct uses the "customer-defined" bit to redistribute bits of "ordinary" <c>HRESULT</c> as follows:
    /// 16 bits for facility, 8 bits for code, the predefined S bit to indicate success/failure and the predefined
    /// X bit to indicate level (low / high).  Valid values are thus 0 - 65535 for facility and 0 - 255 for code.
    /// </remarks>
    public readonly struct QHResult : IEquatable<QHResult>
    {
        private readonly uint Value;

        private QHResult(uint value) => Value = value;

        /// <summary>
        /// Constructor.  Internal because all values must be predefined in this assembly.
        /// </summary>
        /// <param name="severity">Code severity.</param>
        /// <param name="facility">The facility code; must be in range 0-65535.</param>
        /// <param name="code">Error code; must be in ragne 0-255.</param>
        /// <exception cref="ArgumentOutOfRangeException">When facility or code are outside of their allowed ranges.</exception>
        internal QHResult(QHSeverity severity, int facility, int code) {
            if (facility < 0 || facility > 65535)
                throw new ArgumentOutOfRangeException(nameof(facility));
            if (code < 0 || code > 255)
                throw new ArgumentOutOfRangeException(nameof(code));

            Value = (uint)severity | ((uint)facility << 8) | (uint)code;
        }

        public int Facility => (int)((Value >> 8) & 0xFFFF);
        public int Code => (int)(Value & 255);
        
        public bool IsInformation => IsX((uint)QHSeverity.Information);
        public bool IsWarning => IsX((uint)QHSeverity.Warning);
        public bool IsError => IsX((uint)QHSeverity.Error);
        public bool IsCritical => IsX((uint)QHSeverity.Critical);
        public bool IsErrorOrCritical => (Value & ((uint)QHSeverity.Error | (uint)QHSeverity.Critical)) != 0;

        private bool IsX(uint x) => (Value & x) == x;

        public override string ToString() => string.Format("{0}-{1:X8}",
            IsCritical ? "C" : IsError ? "E" : IsWarning ? "W" : "I",
            Value);

        /// <summary>
        /// Conversion to <see cref="Schemas.Core.Eventing.EventId"/> that allows for 6 bits of additional
        /// data to be put in event id's flags (top two bits map severity).
        /// </summary>
        /// <param name="flags">Additional bits to pack into event id; default is 0.</param>
        /// <returns>
        /// An instance of <see cref="Schemas.Core.Eventing.EventId"/> that preserves facility, error code and severity.
        /// </returns>
        public EventId ToEventId(byte flags = 0) {
            if (flags < 0 || flags > 63)
                throw new ArgumentOutOfRangeException(nameof(flags));
            flags |= IsCritical ? EventSeverity.Error :
                IsError ? EventSeverity.High :
                IsWarning ? EventSeverity.Normal :
                EventSeverity.Low;
            return new EventId(flags, (int)(Value & 0xFFFFFF));
        }

        /// <summary>
        /// Constructor from raw <c>HRESULT</c> value.  Mainly used for easy decoding of <c>HRESULT</c>s that
        /// are part of the exception to determine whether the error is "critical" or not.
        /// </summary>
        public static QHResult FromHResult(int hresult) => new QHResult((uint)hresult);
        
        public static implicit operator int(QHResult hr) => unchecked((int)hr.Value);
        public static implicit operator EventId(QHResult hr) => hr.ToEventId(0);

        public bool Equals(QHResult other) => Value == other.Value;
        public override bool Equals(object other) => other is QHResult hr && Equals(hr);
        public override int GetHashCode() => (int)(Value ^ 0x3DA1592B);
    }
}
