using System;
using System.Collections.Generic;
using System.Linq;

namespace Quine.HRCatalog
{
    /// <summary>
    /// Error codes thrown when an  unrecoverable condition has been detected.  These should be
    /// used in combination with <see cref="StunException"/>.  Messages are roughly partitioned by subsystem
    /// (assembly); one subsystem will rarely need multiple "bug" messages.  A new message should be defined
    /// only when automated processing must distinguish it from others.
    /// </summary>
    /// <remarks>
    /// NB! Any message from this facility propagating to the end user should be considered as a bug!
    /// </remarks>
    public static class QHBugs
    {
        static QHBugs() { QHFacilities.ValidateFacility(typeof(QHBugs)); }

        public const int Facility = QHFacilities.Bugs;

        /// <summary>
        /// Checks whether an exception is an irrecoverable one.  The list of exceptions that are treated as "stunned"
        /// is in <see cref="StunnedExceptions"/>.
        /// </summary>
        /// <param name="e">Exception to check.</param>
        /// <returns>
        /// True if <paramref name="e"/> should be treated as a program bug or an irrecoverable error.
        /// </returns>
        public static bool IsStunned(Exception e) => StunnedExceptions.Any(x => x.IsAssignableFrom(e.GetType()));

        /// <summary>
        /// Used by <see cref="IsStunned(Exception)"/> to classify the exception.
        /// </summary>
        public static readonly IReadOnlyCollection<Type> StunnedExceptions = new List<Type>() {
            typeof(ArgumentException),
            typeof(ArithmeticException),
            typeof(ArrayTypeMismatchException),
            typeof(KeyNotFoundException),
            typeof(FormatException),
            typeof(IndexOutOfRangeException),
            typeof(InvalidCastException),
            typeof(System.IO.InvalidDataException),
            typeof(InvalidOperationException),  // Should this be in the list?? Probably yes, since throwing will be redone
            typeof(InvalidProgramException),
            typeof(MemberAccessException),
            typeof(NotImplementedException),    // Also covers StunException
            typeof(NotSupportedException),
            typeof(NullReferenceException),
            typeof(ObjectDisposedException),
        };

        // Values 0-31 reserved for QHEnsure.* methods.
        // All bug values are critical as there's no recovery; the program must be fixed.

        public static readonly QHMessage Stun_NullReference = QHMessage.Critical(Facility, 0,
            "The expression `{0}` was null.");
        public static readonly QHMessage Stun_EmptyString = QHMessage.Critical(Facility, 1,
            "The string expression `{0}` was null, empty or whitespace.");
        public static readonly QHMessage Stun_InvalidValue = QHMessage.Critical(Facility, 2,
            "The value `{0}` did not pass check `{1}`.");
        public static readonly QHMessage Stun_InvalidState = QHMessage.Critical(Facility, 3,
            "Object state check `{0}` failed.");
        public static readonly QHMessage Stun_Nothrow = QHMessage.Critical(Facility, 31,
            "Nothrow contract `{0}` was violated.  The exception was wrapped.");

        /// <summary>
        /// 0: feature name; 1: sentence describing the actual error.
        /// </summary>
        public static readonly QHMessage C_LicenseCheckFailed = QHMessage.Critical(Facility, 32,
            "License check for feature `{0}` failed. {1}");

        /// <summary>
        /// 0: description.
        /// </summary>
        public static readonly QHMessage W_Balked = QHMessage.Warning(Facility, 33,
            "The program balked at unexpected condition: {0}  This problem should be reported to developers.  No other action is needed.");
    }
}
