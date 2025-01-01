using System;
using System.Linq;
using System.Xml.Serialization;
using System.Runtime.Serialization;

/// <summary>
/// Core types used in other type definitions.
/// </summary>
namespace Quine.Schemas.Core
{
    /// <summary>
    /// Thrown by members of <see cref="Rational"/> class on attempt to construct a negative rational number.
    /// </summary>
    public sealed class InvalidRationalNumberException : FormatException {
        internal InvalidRationalNumberException(HRCatalog.QHMessage hMessage, Exception inner = null) 
            : base(hMessage.Message, inner)
        {
            HResult = hMessage.HResult;
        }
    }

    /// <summary>Representation of a positive rational number.</summary>
    [DataContract(Namespace = XmlNamespaces.Core_1_0)]
    [XmlType(Namespace = XmlNamespaces.Core_1_0)]
    [XmlInclude(typeof(TimecodeRate))]
    public partial class Rational
    {
        [DataMember(IsRequired = true)]
        [XmlAttribute]
        public int Num { get; set; }

        [DataMember(IsRequired = true)]
        [XmlAttribute]
        public int Den { get; set; }

        [OnDeserialized]
        private void OnDeserializedCB(StreamingContext _) {
            if (Num < 0 || Den <= 0)
                throw new InvalidRationalNumberException(HRCatalog.QHSchemas.Core.RationalNumber_SignFormat);
        }

        public override string ToString() {
            return String.Format("{0}/{1}", Num, Den);
        }

        // https://rosettacode.org/wiki/Convert_decimal_number_to_rational#C.23
        public static Rational FromDouble(double f) {
            if (f < 0)
                throw new InvalidRationalNumberException(HRCatalog.QHSchemas.Core.RationalNumber_SignFormat);

            int d = 1;
            while (f != Math.Floor(f)) { d <<= 1; f *= 2; }
            int n = (int)f;
            int g = GCD(n, d);
            return new Rational {
                Num = n / g,
                Den = d / g
            };
        }

        /// <summary>
        /// Computes greatest common divisor of two integers.
        /// </summary>
        public static int GCD(int a, int b) {
            if (a < 0 || b <= 0)
                throw new InvalidRationalNumberException(HRCatalog.QHSchemas.Core.RationalNumber_SignFormat);

            do {
                var r = a % b;
                a = b;
                b = r;
            } while (b > 1);
            return a;
        }
    }

#if false
    /// <summary>
    /// Representation of a file hash. Algorithm must be given as a recognizable text string.
    /// </summary>
    [DataContract(Namespace = XmlNamespaces.Core_1_0)]
    [XmlType(Namespace = XmlNamespaces.Core_1_0)]
    public partial class Hash : IEquatable<Hash>
    {
        [Obsolete("DO NOT USE, for compatibility with System.Xml.Serialization and import of QB data.")]
        public Hash() { }

        public Hash(string algorithm, byte[] value) {
            if (string.IsNullOrEmpty(algorithm))
                throw new ArgumentNullException(nameof(algorithm));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (value.Length < 4)
                throw new ArgumentException(nameof(value), "Hash value must have at least 4 bytes.");
            
            this.Algorithm = algorithm;
            this.Value = value;
        }

        [DataMember, XmlAttribute]
        public string Algorithm { get; set; }

        [DataMember, XmlText(DataType = "hexBinary")]
        public byte[] Value { get; set; }

        #region Equality

        public bool Equals(Hash other) {
            if (other is null) return false;
            return Algorithm.ToUpper() == other.Algorithm.ToUpper() && Value.SequenceEqual(other.Value);
        }

        public override bool Equals(object other) {
            return this.Equals(other as Hash);
        }

        public override int GetHashCode() {
            return BitConverter.ToInt32(Value, 0);
        }

        public static bool operator==(Hash o1, Hash o2) {
            return (o1 is null) ? (o2 is null) : o1.Equals(o2);
        }

        public static bool operator!=(Hash o1, Hash o2) {
            return !(o1 == o2);
        }

        #endregion
    }

    [DataContract(Namespace = XmlNamespaces.Core_1_0)]
    [XmlType(Namespace = XmlNamespaces.Core_1_0)]
    public class IdMapping
    {
        [DataMember, XmlAttribute]
        public Guid From { get; set; }

        [DataMember, XmlAttribute]
        public Guid To { get; set; }
    }

    /// <summary>
    /// Value of a single parameter.  It is a dimension, with obligatory parameter name.
    /// </summary>
    /// <typeparam name="T">Type of the parameter.</typeparam>
    [DataContract(Namespace = XmlNamespaces.Core_1_0)]
    [XmlType(Namespace = XmlNamespaces.Core_1_0)]
    public partial class Parameter<T> : Dimension<T>
    {
        [DataMember, XmlAttribute]
        public string Key { get; set; }
    }
#endif
}
