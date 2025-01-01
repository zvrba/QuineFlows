using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Quine.Schemas.Core;

/// <summary>
/// Provides methods that determine rules for string normalization, comparisons, etc. used by <see cref="NormalString{TTraits}"/>.
/// This interface should be implemented by a stateless struct.  All implementations must add themselves to <see cref="KnownTypes"/>
/// in their static ctor so that <see cref="NormalString{TTraits}"/> can be serialized and deserialized without error.
/// </summary>
public interface INormalStringTraits
{
    internal static readonly HashSet<Type> KnownTypes = new();
    
    public abstract static StringComparer StringComparer { get; }
    public abstract static bool EmptyIsNull { get; }
    public abstract static bool AllowNull { get; }
    public abstract static bool CompressWhitespace { get; }

    public virtual static NormalizationForm NormalizationForm => NormalizationForm.FormC;
    public virtual static char[] ForbiddenCharacters => null;
    public virtual static int MaxLength => int.MaxValue;
    public virtual static bool IsUTF8 => false;

    private static readonly Regex WsSequence = new Regex(@"\s\s+", RegexOptions.Compiled);

    internal static void EnsureLength<TSelf>(string input) where TSelf : INormalStringTraits {
        if (input is not null) {
            var len = TSelf.IsUTF8 ? Encoding.UTF8.GetByteCount(input) : input.Length;
            if (len > TSelf.MaxLength)
                throw new InvalidDataException($"Value exceeds the maximum allowed string length by {typeof(TSelf).FullName}.");
        }
    }

    internal static string Normalize<TSelf>(string input) where TSelf : INormalStringTraits {
        if (input is null)
            goto null_result;

        if (TSelf.NormalizationForm != default)
            input = input.Normalize(TSelf.NormalizationForm).Trim();

        if (TSelf.CompressWhitespace)
            input = WsSequence.Replace(input, " ");
        
        if (input.Length == 0) {
            if (TSelf.EmptyIsNull)
                goto null_result;
            return input;
        }

        EnsureLength<TSelf>(input);

        if (TSelf.ForbiddenCharacters is not null) {
            var badChar = input.IndexOfAny(PathComponents.ForbiddenChars);
            if (badChar >= 0) {
                var c = input[badChar];
                var m = string.Format("Found forbidden character `{0}` (code 0x{1:X4}) in normalized string `{2}`.", c, (int)c, input);
                throw new InvalidDataException(m);
            }
        }

        return input;

    null_result:
        if (!TSelf.AllowNull)
            throw new InvalidDataException($"{typeof(TSelf).FullName} does not allow null.");
        return null;
    }
}

