using System;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace Quine.Schemas.Core
{
    [DataContract(Namespace = XmlNamespaces.Core_1_0)]
    [XmlType(Namespace = XmlNamespaces.Core_1_0)]
    public class TimelinePoint
    {
        [DataMember]
        public TimecodeTime Timecode { get; set; }

        [DataMember]
        public long? SamplesSinceMidnight { get; set; }

        [DataMember]
        public double? SecondsSinceMidnight { get; set; }

        public static TimelinePoint From(TimecodeTime tc) {
            return new TimelinePoint {
                Timecode = tc,
                SecondsSinceMidnight = tc.ToSeconds(false)
            };
        }
        public static TimelinePoint From(TimecodeRate tcr, TimecodeTime tc) {
            var ssm = tc.ToFrameNumber(tcr);
            return new TimelinePoint {
                Timecode = tc,
                SamplesSinceMidnight = ssm,
                SecondsSinceMidnight = ssm / tcr.ClockRate
            };
        }
        public static TimelinePoint From(TimecodeRate tcr, long sampleCount) {
            return new TimelinePoint {
                Timecode = tcr != null ? TimecodeTime.From(tcr, sampleCount) : null,
                SamplesSinceMidnight = sampleCount,
                SecondsSinceMidnight = sampleCount / tcr?.ClockRate
            };
        }
        public static TimelinePoint From(TimecodeRate tcr, double secondsSinceMidnight) {
            var tc = tcr != null ? TimecodeTime.From(tcr, (long)Math.Round(secondsSinceMidnight * tcr.ClockRate)) : null;
            return new TimelinePoint {
                Timecode = tc,
                SamplesSinceMidnight = tc?.ToFrameNumber(tcr),
                SecondsSinceMidnight = secondsSinceMidnight
            };
        }
    }
}
