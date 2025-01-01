using System;

namespace Quine.HRCatalog;

/// <summary>
/// <para>
/// Thrown when a program gets "stunned" on a bug, with no means to recover.
/// This covers all errors detected at run-time, both usage errors and conditions that cannot be handled.
/// The class is sealed and cannot be customized.  When customization is needed, a new exception type
/// and error code should be defined in a facility other than <see cref="QHBugs"/>.
/// </para>
/// <para>
/// USAGE GUIDELINE: this exception propagating to the end-user should be considered a program bug.
/// </para>
/// <para>
/// The exception derives from <see cref="NotImplementedException"/> since the condition is detected,
/// but it is not anticipated that it can be handled in any sane way.
/// </para>
/// </summary>
public sealed class StunException : NotImplementedException
{
    public StunException(QHMessage hMessage, Exception inner, params object[] args)
        : base(hMessage.Format(args), inner)
    {
        if (hMessage.HResult == QHBugs.Stun_Nothrow.HResult && inner == null)
            throw new ArgumentException(nameof(QHBugs.Stun_Nothrow) + " HRESULT must provide inner exception.");
        HResult = hMessage.HResult;
    }
}

#if false
namespace Extensions {
    public static class ExceptionExtensions {
        /// <summary>
        /// Check whether <c>this</c> should be treated as an irrecoverable error.
        /// </summary>
        public static bool IsStunned(this Exception @this) => QHBugs.IsStunned(@this);

        /// <summary>
        /// Checks whether <c>this</c> has a <c>HRESULT</c> code corresponding to critical level.
        /// </summary>
        public static bool IsCritical(this Exception @this) => QHResult.FromHResult(@this.HResult).IsCritical;
    }
}
#endif
