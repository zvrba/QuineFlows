using System;
using System.Runtime.CompilerServices;

namespace Quine.HRCatalog;

/// <summary>
/// Utility methods for simplified checking of invariants.
/// This should be used only to report program bugs, not user-correctable errors!
/// </summary>
public static class QHEnsure
{
    public static T NotNull<T>(T value, [CallerArgumentExpression("value")] string expression = null) where T : class =>
        value ?? throw new StunException(QHBugs.Stun_NullReference, null, expression);

    public static string NotEmpty(string value, [CallerArgumentExpression("value")] string expression = null) =>
        !string.IsNullOrWhiteSpace(value) ? value : throw new StunException(QHBugs.Stun_EmptyString, null, expression);

    public static T Value<T>(T value, bool isValid, [CallerArgumentExpression("isValid")] string expression = null) =>
        isValid ? value : throw new StunException(QHBugs.Stun_InvalidValue, null, value, expression);

    public static void State(bool isValid, [CallerArgumentExpression("isValid")] string expression = null) {
        if (!isValid)
            throw new StunException(QHBugs.Stun_InvalidState, null, expression);
    }

    public static void NoThrow(Action a, [CallerArgumentExpression("a")] string expression = null) {
        try {
            a();
        }
        catch (Exception e) {
            throw new StunException(QHBugs.Stun_Nothrow, e, expression);
        }
    }

    public static T NoThrow<T>(Func<T> a, [CallerArgumentExpression("a")] string expression = null) {
        try {
            return a();
        }
        catch (Exception e) {
            throw new StunException(QHBugs.Stun_Nothrow, e, expression);
        }
    }

    // TODO: Multi-argument overloads so that no lambda has to be allocated??
}
