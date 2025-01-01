using System;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace Quine.Schemas.Core
{
    /// <summary>
    /// Timecode rate (fps) is a rational number, possibly with drop frames and sub frames (unsupported).
    /// </summary>
    [DataContract(Namespace = XmlNamespaces.Core_1_0)]
    [XmlType(Namespace = XmlNamespaces.Core_1_0)]
    public class TimecodeRate : Rational
    {
        [DataMember, XmlAttribute]
        public int SubFrame { get; set; }

        [DataMember, XmlAttribute]
        public bool Drop { get; set; }

        /// <summary>
        /// Rate as double number.
        /// </summary>
        [XmlIgnore]
        public double ClockRate { get { return (double)Num / Den; } }

        /// <summary>
        /// Integral fps, rounded up.
        /// </summary>
        [XmlIgnore]
        internal int Fps { get { return (int)Math.Ceiling((double)Num / Den); } }

        /// <summary>
        /// Maximum frame number; <see cref="TimecodeTime.ToFrameNumber(TimecodeRate)"/> will return a
        /// number up to but not including this value.
        /// </summary>
        [XmlIgnore]
        public double MaxFrame { get { return TimecodeTime.From(24, 0, 0, 0).ToFrameNumber(this); } }

        public override string ToString() {
            return String.Format("{0}/{1}{2}", Num, Den, Drop ? "DF" : "");
        }

        public static TimecodeRate Fps24 { get { return new TimecodeRate { Num = 24, Den = 1, Drop = false }; } }
        public static TimecodeRate Fps25 { get { return new TimecodeRate { Num = 25, Den = 1, Drop = false }; } }
        public static TimecodeRate Fps30 { get { return new TimecodeRate { Num = 30, Den = 1, Drop = false }; } }
        public static TimecodeRate Fps2397 { get { return new TimecodeRate { Num = 24000, Den = 1001, Drop = false }; } }
        public static TimecodeRate Fps2497 { get { return new TimecodeRate { Num = 25000, Den = 1001, Drop = false }; } }
        public static TimecodeRate Fps2997DF { get { return new TimecodeRate { Num = 30000, Den = 1001, Drop = true }; } }
        public static TimecodeRate Fps2997NDF { get { return new TimecodeRate { Num = 30000, Den = 1001, Drop = false }; } }

        public static TimecodeRate FromRational(Rational rational) {
            return new TimecodeRate() {
                Num = rational.Num,
                Den = rational.Den,
                Drop = false,
                SubFrame = 0
            };
        }

        /// <summary>
        /// Approximate conversion of fractional framerates to <c>TimeCodeRate</c> with attempted guessing
        /// of SMPTE framerates: if a fractional framerate rounds UP (<see cref="Math.Ceiling(double)"/>)
        /// to 24, 25, or 30 a SMPTE framerate of 24000/1001, 25000/1001 and 30000/1001 is returned; the
        /// latter ALWAYS being assumed to be with drop frame.
        /// </summary>
        new public static TimecodeRate FromDouble(double d) {
            int num = 0, den = 0;

            switch (Math.Round(d)) {
                case 24.0:
                    if (d < 24) { num = 24000; den = 1001; } else { num = 24; den = 1; };
                    break;

                case 25.0:
                    if (d < 25) { num = 25000; den = 1001; } else { num = 25; den = 1; }
                    break;

                case 30.0:
                    if (d < 30)
                        return new TimecodeRate { Num = 30000, Den = 1001, Drop = true };
                    return new TimecodeRate { Num = 30, Den = 1 };

                default: {
                        var r = Rational.FromDouble(d);
                        num = r.Num; den = r.Den;
                    }
                    break;
            }
            return new TimecodeRate { Num = num, Den = den, Drop = false };
        }
    }
}
