using System;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace Quine.Schemas.Core
{
    /// <summary>
    /// XmlSerializer cannot serialize DateTimeOffset, so we break it down into two parts.
    /// Explicit conversions to and from DateTimeOffset are also defined.
    /// </summary>
    [DataContract(Namespace = XmlNamespaces.Core_1_0)]
    [XmlType(Namespace = XmlNamespaces.Core_1_0)]
    public class Timestamp
    {
        [DataMember, XmlAttribute]
        public short TzOffset { get; set; }

        [DataMember, XmlText(DataType = "dateTime")]
        public DateTime LocalFiletime { get; set; }

        public static explicit operator DateTimeOffset(Timestamp ts) {
            return new DateTimeOffset(
                ts.LocalFiletime.Ticks,
                TimeSpan.FromMinutes(ts.TzOffset));
        }

        public static explicit operator Timestamp(DateTimeOffset dt) {
            return new Timestamp {
                LocalFiletime = dt.DateTime,
                TzOffset = checked((short)dt.Offset.TotalMinutes)
            };
        }
    }

}
