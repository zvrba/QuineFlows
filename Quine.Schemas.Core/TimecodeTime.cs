using System;
using System.Linq;
using System.Xml.Serialization;
using System.Runtime.Serialization;

namespace Quine.Schemas.Core
{
    /// <summary>
    /// Representation of a timecode, with optional frame number.  This is separated from
    /// <see cref="TimecodeRate"/> as TC time can often be used without rate.
    /// </summary>
    [DataContract(Namespace = XmlNamespaces.Core_1_0)]
    [XmlType(Namespace = XmlNamespaces.Core_1_0)]
    public class TimecodeTime : IEquatable<TimecodeTime>
    {
        [DataMember, XmlAttribute]
        public sbyte H { get; set; }

        [DataMember, XmlAttribute]
        public sbyte M { get; set; }

        [DataMember, XmlAttribute]
        public sbyte S { get; set; }

        [DataMember, XmlAttribute]
        public int F { get; set; }

        public bool Equals(TimecodeTime tc) {
            return H == tc.H && M == tc.M && S == tc.S && F == tc.F;
        }

        public override bool Equals(object obj) {
            if (obj is TimecodeTime tc)
                return Equals(tc);
            return false;
        }

        public override int GetHashCode() {
            return (H + M + S) * F;
        }

        public override string ToString() {
            return String.Format("{0:D2}:{1:D2}:{2:D2}:{3:D2}", H, M, S, F);
        }
        
        public static TimecodeTime Parse(string v) {
            var f = v.Split(':');
            if (f.Length != 4)
                throw new InvalidRationalNumberException(HRCatalog.QHSchemas.Core.Timecode_InvalidFormat);
            try {
                return TimecodeTime.From(
                    int.Parse(f[0]),
                    int.Parse(f[1]),
                    int.Parse(f[2]),
                    int.Parse(f[3])
                );
            }
            catch (Exception e) {
                throw new InvalidRationalNumberException(HRCatalog.QHSchemas.Core.Timecode_InvalidFormat, e);
            }
        }

        public static TimecodeTime ParseSmpte331m(int[] bytes, out bool drop) {
            drop = (bytes[0] & 64) != 0;
            int frames = ((bytes[0] >> 4) & 3) * 10 + (bytes[0] & 15);
            int seconds = ((bytes[1] >> 4) & 7) * 10 + (bytes[1] & 15);
            int minutes = ((bytes[2] >> 4) & 7) * 10 + (bytes[2] & 15);
            int hours = ((bytes[3] >> 4) & 3) * 10 + (bytes[3] & 15);
            return From(hours, minutes, seconds, frames);
        }

        public static TimecodeTime TryParseLtcChange(string ltcChange, out bool drop) {
            try {
                int[] bytes = new int[4];
                bytes[0] = int.Parse(ltcChange.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
                bytes[1] = int.Parse(ltcChange.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
                bytes[2] = int.Parse(ltcChange.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
                bytes[3] = int.Parse(ltcChange.Substring(6, 2), System.Globalization.NumberStyles.HexNumber);
                return ParseSmpte331m(bytes, out drop);
            }
            catch {
                drop = false;
                return null;
            }
        }

        public static TimecodeTime TryParse(string v) {
            try {
                return Parse(v);
            }
            catch (Exception) {
                return null;
            }
        }

        public static TimecodeTime From(long h, long m, long s, long f) {
            if (h < 0 || m < 0 || m > 59 || s < 0 || s > 59)
                throw new InvalidRationalNumberException(HRCatalog.QHSchemas.Core.Timecode_InvalidFields);
            return new TimecodeTime {
                H = (sbyte)h,
                M = (sbyte)m,
                S = (sbyte)s,
                F = (int)f
            };
        }

        public static TimecodeTime From(TimecodeRate tcr, long frameNumber) {
            double fps_d = (double)tcr.Num / tcr.Den;
            int fps_i = (int)Math.Ceiling(fps_d);

            // there are 17982 frames in 10 min @ 29.97df
            int df_fp10m = 17982;

            if (tcr.Drop) {
                var fn = frameNumber;

                long D = fn / df_fp10m;
                long M = fn % df_fp10m;

                fn += (18 * D) + (2 * ((M - 2) / 1798));
                return TimecodeTime.From(
                    (((fn / 30) / 60) / 60),
                    ((fn / 30) / 60) % 60,
                    (fn / 30) % 60,
                    fn % 30
                );
            } else {
                long frames_per_hour = 3600 * fps_i;
                long hour = frameNumber / frames_per_hour;
                long timecode_frames_left = frameNumber % frames_per_hour;
                long minute = timecode_frames_left / (fps_i * 60);
                timecode_frames_left = timecode_frames_left % (fps_i * 60);
                long second = timecode_frames_left / fps_i;
                long frame = timecode_frames_left % fps_i;
                return TimecodeTime.From(hour, minute, second, frame);
            }
        }

        public TimecodeTime Inc(TimecodeRate r) {
            var ret = TimecodeTime.From(H, M, S, F);
            if (++ret.F < r.Fps) return this;
            ret.F = 0;
            if (++ret.S < 60) return this;
            ret.S = 0;
            if (++ret.M < 60) return this;
            ret.M = 0;
            if (++ret.H < 24) return this;
            ret.H = 0;

            if (r.Drop &&        // Skip TC if DF,
                ret.F == 0 &&         // 1) this is a droppable frame (0; just after "carry over"),
                ret.S == 0 &&        // 2) full minute, first second,
                (ret.M % 10 != 0))   // 3) minute not divisible by 10
                ret.F = 2;

            return ret;
        }

        public TimecodeTime Dec(TimecodeRate r) {
            var ret = TimecodeTime.From(H, M, S, F);
            if (!r.Drop ||       // Ordinary decrement if: 1) not DF TC,
                ret.F > 2 ||          // 2) not a droppable frame (0 and 1)
                ret.S > 0 ||         // 3) not a full minute
                (ret.M % 10 == 0))   // 4) not a droppable minute
                if (--ret.F != sbyte.MaxValue) return this;

            ret.F = r.Fps - 1;

            if (--ret.S != sbyte.MaxValue) return this;
            ret.S = 59;
            if (--ret.M != sbyte.MaxValue) return this;
            ret.M = 59;
            if (--ret.H != sbyte.MaxValue) return this;
            ret.H = 23;

            return ret;
        }

        public long ToFrameNumber(TimecodeRate tcr) {
            double fps_d = (double)tcr.Num / tcr.Den;
            long fps_i = (long)Math.Ceiling(fps_d);
            if (tcr.Drop) {
                long minutes = 60 * H + M;
                long frameNumber = fps_i * 3600 * H + fps_i * 60 * M
                    + fps_i * S + F - 2 * (minutes - minutes / 10);
                return frameNumber;
            } else {
                return (((H * 60 * 60) + (M * 60) + S) * fps_i) + F;
            }
        }

        public (int, int, int, int) ToTuple() {
            return (H, M, S, F);
        }

        /// <summary>
        /// Convert the timecode to seconds since midnight
        /// </summary>
        /// <param name="tcr">TimecodeRate used for the conversion</param>
        /// <returns></returns>
        public double ToSeconds(TimecodeRate tcr) {
            long frameNumber = ToFrameNumber(tcr);
            return 86400 * frameNumber / tcr.MaxFrame;
        }

        /// <summary>
        /// Convert the timecode to seconds since midnight, 
        /// rounding the framecounter based on roundUp parameter
        /// </summary>
        /// <param name="roundUp">When true; add one second if framecounter >= 1. When false, ignore framecounter</param>
        /// <returns></returns>
        public int ToSeconds(bool roundUp) {
            return ((H * 60 * 60) + (M * 60) + S) + (roundUp ? (F == 0 ? 0 : 1) : 0);
        }

        // Imprecise comparison of TimecodeTime objects, returning the lower of the two.
        // Only returning "correct" result if the two TimecodeTime objects are of the same framerate.
        public static TimecodeTime GetLowerTct(TimecodeTime a, TimecodeTime b) {
            if (a == null && b != null) return b;
            if (a != null && b == null) return a;
            if (a == null && b == null) return null;
            return a.ToTuple().CompareTo(b.ToTuple()) < 0 ? a : b;
        }
    }
}
