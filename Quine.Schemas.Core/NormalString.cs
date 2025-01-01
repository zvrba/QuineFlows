using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;

namespace Quine.Schemas.Core;

// See https://docs.microsoft.com/en-us/dotnet/standard/base-types/best-practices-strings
// Conventions for structs instantiated with concrete INormalStringTraits:
// WSI = whitespace insensitive (alt: WS = whitespace sensitive)
// CI = case insensitive (alt: CS = case sensitive)
// EN = empty is null (alternative: EE = empty is empty)
// FS = only characters allowed by filesystems are allower (alternative: empty)
// (optional max length suffix, e.g. _384)
// E.G.: String_WSI_CI_EN_FS

/// <summary>
/// This struct represents a normalized string as determined by <typeparamref name="TStringTraits"/>.
/// Instances may be created only by implicit conversion from string, while value is extracted by implicit
/// conversion to string.
/// </summary>
[DataContract(Namespace = XmlNamespaces.Core_1_0)]
[KnownType("GetKnownTypes")]
public readonly struct NormalString<TStringTraits> :
    IEquatable<NormalString<TStringTraits>>,
    IComparable<NormalString<TStringTraits>>
    where TStringTraits : struct, INormalStringTraits
    // NB! Does NOT implement IContentAddressable: ContentAddress has special-case for NormalString<>
{
    private static IEnumerable<Type> GetKnownTypes() => INormalStringTraits.KnownTypes;

    static NormalString() {
        INormalStringTraits.KnownTypes.Add(typeof(NormalString<TStringTraits>));
    }

    [DataMember(Name = "Data")]
    private readonly string data;

    private NormalString(string input) => data = INormalStringTraits.Normalize<TStringTraits>(input);

    [OnDeserialized]
    void OnDeserializedCB(StreamingContext _) {
        var n = INormalStringTraits.Normalize<TStringTraits>(data);
        if (!TStringTraits.StringComparer.Equals(data, n))
            throw new InvalidDataException("Found invalid data in serialized NormalString.");
    }
    
    /// <summary>
    /// True if the data is null.  This is a convenience member as structs can't be directly compared with <c>null</c>.
    /// </summary>
    public bool IsNull => data == null;

    /// <summary>
    /// True if the data is null or empty string.  This is a convenience member as structs can't be directly compared with <c>null</c>.
    /// </summary>
    public bool IsNullOrEmpty => data == null || data.Length == 0;

    /// <summary>
    /// Simple accessor for the value itself, to avoid use of casts.
    /// </summary>
    public string Value => data;

    public int CompareTo(NormalString<TStringTraits> other) => TStringTraits.StringComparer.Compare(data, other.data);
    public bool Equals(NormalString<TStringTraits> other) => TStringTraits.StringComparer.Equals(data, other.data);
    public override bool Equals(object obj) => obj is NormalString<TStringTraits> ns ? Equals(ns) : false;
    public override int GetHashCode() => TStringTraits.StringComparer.GetHashCode(data);
    public override string ToString() => data;

    public static bool operator ==(NormalString<TStringTraits> s1, NormalString<TStringTraits> s2) => s1.Equals(s2);
    public static bool operator !=(NormalString<TStringTraits> s1, NormalString<TStringTraits> s2) => !s1.Equals(s2);

    public static implicit operator NormalString<TStringTraits>(string input) => new(input);
    public static implicit operator string(NormalString<TStringTraits> normalString) => normalString.data;
}
