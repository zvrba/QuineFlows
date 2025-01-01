using System;

namespace Quine.HRCatalog
{
    /// <summary>
    /// Combines a hresult code with a string format message.  Used to build the message catalog which in turn will
    /// make it possible to look up help text.
    /// </summary>
    /// <remarks>
    /// It is recommended that custom, user-facing exceptions take an instance of <see cref="QHMessage"/> as argument.
    /// The exception's constructor must set <see cref="Exception.HResult"/> to <see cref="HResult"/> casted to int.
    /// If <see cref="ExceptionMessage"/> contains format parameters, the custom exception must override
    /// <see cref="Exception.Message"/> property to fill in the parameters.
    /// </remarks>
    public readonly struct QHMessage
    {
        /// <summary>
        /// Message code.
        /// </summary>
        public readonly QHResult HResult;


        /// <summary>
        /// Conversion to <c>EventId</c> for interpreting traces.
        /// </summary>
        public Schemas.Core.Eventing.EventId EventId => HResult;

        /// <summary>
        /// Message text prefixed with error codes in standard format.
        /// The text must be suitable for sending to <see cref="string.Format(string, object[])"/>.
        /// </summary>
        public readonly string Message;

        private QHMessage(QHResult hResult, string message) {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentNullException(nameof(message));
            HResult = hResult;
            Message = string.Format("{0}: {1}", HResult, message);
        }

        /// <summary>
        /// Convenience method that forwards <see cref="Message"/> and <paramref name="args"/> to
        /// <see cref="string.Format(string, object[])"/>.
        /// </summary>
        /// <returns>The formatted string.</returns>
        public string Format(params object[] args) => string.Format(Message, args);

        public static QHMessage Information(int facility, int code, string format) => new QHMessage(new QHResult(QHSeverity.Information, facility, code), format);
        public static QHMessage Warning(int facility, int code, string format) => new QHMessage(new QHResult(QHSeverity.Warning, facility, code), format);
        public static QHMessage Error(int facility, int code, string format) => new QHMessage(new QHResult(QHSeverity.Error, facility, code), format);
        public static QHMessage Critical(int facility, int code, string format) => new QHMessage(new QHResult(QHSeverity.Critical, facility, code), format);
    }
}
