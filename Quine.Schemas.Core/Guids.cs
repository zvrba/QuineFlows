using System;
using System.Collections.Generic;
using System.Text;

namespace Quine.Schemas.Core
{
    // TODO:  Implement v6 guid with Gregorian epoch.
    // See: https://datatracker.ietf.org/doc/html/draft-peabody-dispatch-new-uuid-format

    /// <summary>
    /// Various guid utility methods.
    /// </summary>
    public static class Guids
    {
        /// <summary>
        /// Converts a big-endian byte string (directly converted from string representation) to Guid.
        /// No format checks are performed, except as documented by thrown exceptions.
        /// </summary>
        /// <param name="bytes">Byte array to convert to guid.</param>
        /// <param name="copy">If true (the default), the array is copied; otherwise it is mutated in-place.</param>
        /// <exception cref="ArgumentNullException">If passed null.</exception>
        /// <exception cref="ArgumentException">If the array is not exactly 16 bytes long.</exception>
        public static Guid FromBytesBE(byte[] bytes, bool copy = true) {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length != 16)
                throw new ArgumentException("The array is not exactly 16 bytes long.", nameof(bytes));

            bytes = ReverseGuidBytes(bytes, copy);
            return new Guid(bytes);
        }

        /// <summary>
        /// Converts a guid to big-endian byte array.  This is also the internal storage format used by SQL server
        /// (i.e., what is obtained by casting to/from <c>BINARY(16)</c>).
        /// </summary>
        /// <param name="guid">Guid to convert.</param>
        /// <returns>Big-endian byte array.</returns>
        public static byte[] ToBytesBE(Guid guid) {
            var bytes = guid.ToByteArray();
            return ReverseGuidBytes(bytes, false);
        }

        /// <summary>
        /// Creates a name-based GUID according to RFC4122.
        /// </summary>
        /// <param name="nsId">Namespace id.</param>
        /// <param name="name">The name converted to a canonical byte string.</param>
        /// <returns>A guid created from the namespace id and name bytes, using MD5 hash algorithm.</returns>
        public static Guid FromNameMD5(Guid nsId, byte[] name) {
            var nsBytes = ToBytesBE(nsId);
            byte[] md;

            using (var h = System.Security.Cryptography.MD5.Create()) {
                h.TransformBlock(nsBytes, 0, 16, nsBytes, 0);
                h.TransformFinalBlock(name, 0, name.Length);
                md = h.Hash;
            }

            md[6] &= 0x0F; md[6] |= 0x30;   // Version 3
            md[8] &= 0x3F; md[8] |= 0x80;   // IETF variant
            return FromBytesBE(md, false);  // No need to copy
        }

        private static byte[] ReverseGuidBytes(byte[] bytes, bool copy) {
            var b = copy ? new byte[16] : bytes;
            if (copy)
                bytes.CopyTo(b, 0);
            
            Array.Reverse(b, 0, 4);
            Array.Reverse(b, 4, 2);
            Array.Reverse(b, 6, 2);
            
            return b;
        }
    }
}
