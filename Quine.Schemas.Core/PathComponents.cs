using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Quine.Schemas.Core
{
    /// <summary>
    /// Exception thrown by <see cref="PathComponents"/> methods when an invalid path is detected.
    /// </summary>
    public sealed class PathFormatException : FormatException
    {
        private readonly Lazy<string> message;

        internal PathFormatException(HRCatalog.QHMessage hMessage, string path, char? forbiddenCharacter = null) :
            base(hMessage.Message)
        {
            HResult = hMessage.HResult;
            Path = path;
            ForbiddenCharacter = forbiddenCharacter;
            message = new Lazy<string>(GetMessage);
        }

        /// <summary>
        /// Path that triggered the exception.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Forbidden character found in the path, if any.
        /// </summary>
        public char? ForbiddenCharacter { get; }

        /// <inheritdoc/>
        public override string Message => message.Value;

        private const string InvalidPathFormat = "Path `{0}` is invalid: ";

        private string GetMessage() {
            if (HResult == HRCatalog.QHSchemas.Core.PathFormat_ForbiddenCharacter.HResult)
                return string.Format(InvalidPathFormat + base.Message, Path, ForbiddenCharacter, (int)ForbiddenCharacter.Value);
            return string.Format(InvalidPathFormat + base.Message, Path);
        }
    }

    /// <summary>
    /// Representation of platform-dependent paths in platform-independent manner as a collection of path components
    /// exposed through <c>IEnumerable</c> and array access.  Use <see cref="Make(string)"/> to create an instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If <see cref="IsAbsolute"/> is true, the 0th component of the path is the "root component", otherwise the whole
    /// path is relative.  Supports mixed-separator paths on input; the separator is internally replaced with <see cref="SeparatorChar"/> .
    /// Correctly parses Windows and Unix paths on both platforms.
    /// </para>
    /// <para>
    /// A path MUST not contain empty components, with the exception of Unix absolute paths where the root component
    /// is an empty string. (In string form, this means that a path ending with / or having // in the middle is invalid.)
    /// Supported path patterns are the following:
    /// <list type="bullet">
    /// <item><c>a/b/c</c> is a relative path.</item>
    /// <item><c>/a/b/c</c> is an absolute UNIX path; only "" is the root component.</item>
    /// <item><c>X:/a/b/c</c> is an absolute Windows path with "X:" as the root component.
    /// The slash after colon is obligatory; otherwise the path is drive-relative.</item>
    /// <item><c>//server/share/a/b/c</c> is a Windows SMB-path.  <c>//server/share</c> is the root component, the rest
    /// are relative components.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <see cref="PathFormatException"/> is thrown when invalid paths are given or internal variants violated.
    /// </para>
    /// <para>
    /// This class is needed because <see cref="Path"/> class behaves differently on different platforms.
    /// </para>
    /// </remarks>
    [DataContract(Namespace = XmlNamespaces.Core_1_0)]
    public struct PathComponents : IEnumerable<string>, IEquatable<PathComponents>, IContentAddressable
    {
        void IContentAddressable.AddToContentAddress(ref ContentAddress ca) => ca.Add(IsAbsolute, NormalizedString);

        #region Constants

        /// <summary>
        /// Unique instance of an empty <c>PathComponents</c> object: it has zero components and is not absolute.
        /// An empty <c>PathComponents</c> object cannot be instantiated in any other way.  NB! <c>default</c>
        /// instance is regrettably NOT equal to <c>Empty</c>.
        /// </summary>
        public static readonly PathComponents Empty = new PathComponents(false);

        /// <summary>
        /// A set of characters that must not occur in either native file names or path names in a cross-platform application.
        /// '\' is also included; it may possibly be a valid filename char on unix, but we can't allow it in xplatform app.
        /// </summary>
        // Windows is a superset of unix, copied from https://github.com/dotnet/runtime/blob/4f9ae42d861fcb4be2fcd5d3d55d5f227d30e723/src/libraries/System.Private.CoreLib/src/System/IO/Path.Windows.cs
        public static readonly char[] ForbiddenChars = new char[]
        {
            '\"', '|', '\0', ':', '*', '?', '\\', '/', '<', '>',
            (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
            (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
            (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
            (char)31, 
        };

        /// <summary>
        /// The <c>/</c> character is internally used to separate individual components.
        /// </summary>
        public static readonly char SeparatorChar = '/';

        /// <summary>
        /// True if we're running on Windows.
        /// </summary>
        public static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        #endregion

        #region Member data and serialization

        /// <summary>
        /// Normalized string for serialization; uses '/' as separator character.
        /// </summary>
        [DataMember]
        public readonly string NormalizedString;


        /// <summary>
        /// True if this is an absolute path.
        /// </summary>
        [DataMember]
        public readonly bool IsAbsolute;

        // 0th component is the root when IsAbsolute is true.
        private string[] components;

        /// <summary>
        /// True if this is an empty <c>PathComponents</c> object (<see cref="Empty"/>).
        /// NB! This will return true also for a default instance.
        /// </summary>
        public readonly bool IsEmpty => components == null || (components.Length == 0 && !IsAbsolute);

        /// <summary>
        /// True only for a default instance (i.e., <c>Empty</c> instance will return false).
        /// </summary>
        public readonly bool IsNull => components == null;
        
        /// <summary>
        /// Returns a string representation of this using the platform's native character separator.
        /// </summary>
        public readonly string NativeString => !IsWindows ? NormalizedString : NormalizedString.Replace('/', '\\');

        [OnDeserialized]
        void DeserializeCB(StreamingContext ctx) {
            if (NormalizedString == null) {
                components = Array.Empty<string>();
            } else {    // Must handle SMB paths.
                var p = Make(NormalizedString);
                components = p.components;
            }
            CheckInvariants();
        }

        #endregion

        #region Private constructors and invariants

        private PathComponents(string root, string[] relative) {
            if (root == null) {
                components = relative;
            } else {
                components = new string[relative.Length + 1];
                Array.Copy(relative, 0, components, 1, relative.Length);
                components[0] = root;
            }

            IsAbsolute = root != null;
            NormalizedString = String.Join("" + SeparatorChar, components);
            CheckInvariants();
        }

        // Used to construct the result of any operation.  Therefore it must not allow construction of empty paths.
        private PathComponents(string[] components) {
            this.components = components ?? throw new ArgumentNullException(nameof(components));
            NormalizedString = String.Join("" + SeparatorChar, this.components);
            IsAbsolute = Path.IsPathFullyQualified(NormalizedString);
            CheckInvariants();
        }

        // Helper to create the unique empty instance.  Must have fake argument for overloading.
        private PathComponents(bool unused) {
            this.components = Array.Empty<string>();
            NormalizedString = String.Empty;
            IsAbsolute = false;
            CheckInvariants();
        }

        void CheckInvariants() {
            if (components.Length == 0 && IsAbsolute)
                throw new PathFormatException(HRCatalog.QHSchemas.Core.PathFormat_EmptyAbsolutePath, "");

            for (int i = 0; i < components.Length; ++i) {
                var isRootComponent = IsAbsolute && i == 0;
                var pc = components[i];
                if (string.IsNullOrEmpty(pc) && !isRootComponent)   // Root component of Unix path may be empty
                    throw new PathFormatException(HRCatalog.QHSchemas.Core.PathFormat_EmptyPathComponent, NormalizedString);
                CheckForbiddenChars(pc, isRootComponent);
            }
        }

        // TODO!!!! On Windows, no path component may end with a '.' (e.g., "asdf." is invalid path component).
        // It seems though, that when such paths are attempted to be created, the dot is simply stripped.

        private void CheckForbiddenChars(string s, bool isRoot) {
            // '/' is not allowed char in path components, EXCEPT in the root component of samba path.
            if (isRoot) {
                if (s.StartsWith("//")) s = s.Replace("/", "");     // root component of samba paths
                else s = s.Replace(":", "");                        // : is otherwise forbidden
            }

            int badChar = s.IndexOfAny(ForbiddenChars);
            if (badChar >= 0)
                throw new PathFormatException(HRCatalog.QHSchemas.Core.PathFormat_ForbiddenCharacter, NormalizedString, s[badChar]);
        }

        #endregion

        /// <summary>
        /// Creates an instance.
        /// </summary>
        /// <param name="path">
        /// String representation of a path.  Empty string will return <see cref="Empty"/>.
        /// </param>
        /// <returns>A valid instance representing the path.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> was null.</exception>
        /// <exception cref="PathFormatException"><paramref name="path"/> is invalid.</exception>
        public static PathComponents Make(string path) {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (path.Length == 0)
                return Empty;
            
            path = path.Replace('\\', '/');
            var rootComponent = GetRoot(path);

            // Extract relative part.  Substring() throws if startindex is > length.
            if (rootComponent != null) {
                if (path.Length == rootComponent.Length) path = "";
                else path = path.Substring(rootComponent.Length + 1);
            }
            
            var relativeComponents = Array.Empty<string>();
            if (path.Length > 0)
                relativeComponents = path.Split('/');
            
            return new PathComponents(rootComponent, relativeComponents);
        }

        /// <summary>
        /// Method for extracting the "root" component of a path in platform-independent way.
        /// <see cref="Path.GetPathRoot(string)"/> cannot be used as its results are platform-dependent,
        /// and this method must handle win and unix paths on ANY platform.
        /// </summary>
        /// <remarks>
        /// </remarks>
        /// <returns>
        /// Root component up to, but not including, the following separator char (if any), which is an empty string for
        /// Unix absolute paths.  The method returns null for a relative path.
        /// </returns>
        private static string GetRoot(string path) {
            // Unix or SMB absolute path
            if (path[0] == '/') {
                if (path.Length < 2)
                    throw new PathFormatException(HRCatalog.QHSchemas.Core.PathFormat_ForbidRoot, path);
                if (path[1] != '/') // Not SMB path; unix root path is empty string.
                    return "";

                // SMB path.  Minimal form of a SMB path is //a/b (5 chars); this also names the share root.
                if (path.Length < 5 || path[2] == '/')
                    throw new PathFormatException(HRCatalog.QHSchemas.Core.PathFormat_SmbServerMissing, path);

                int count = 0, last = 0;
                for (int i = 2; count < 2 && i < path.Length; ++i) { // Server name starts at 2
                    if (path[i] == '/') {
                        ++count;
                        last = i;
                    }
                }

                if (count == 1) {   // "//a/b" is valid, but "//a/" is not.
                    if (last == path.Length - 1)
                        throw new PathFormatException(HRCatalog.QHSchemas.Core.PathFormat_SmbShareMissing, path);
                    return path;    // Only one slash found, so share name extends to the end of the string.
                }

                if (path[last - 1] == '/')
                    throw new PathFormatException(HRCatalog.QHSchemas.Core.PathFormat_SmbShareMissing, path);

                return path.Substring(0, last);
            }

            // Drive-based path, but possibly not absolute.
            // Input like "E:" and "E:/" MUST be accepted, but "E:stuff" MUST NOT.
            if (path.Length >= 2 && path[1] == ':') {
                if (path.Length >= 3 && path[2] != '/')
                    throw new PathFormatException(HRCatalog.QHSchemas.Core.PathFormat_ForbidDriveRelative, path);
                var drive = char.ToUpperInvariant(path[0]);
                if (drive < 'A' || drive > 'Z')
                    throw new PathFormatException(HRCatalog.QHSchemas.Core.PathFormat_InvalidDrive, path);
                return path.Substring(0, 2);    // "X:"
            }

            // Must be relative path.
            return null;
        }

        /// <summary>
        /// Returns the concatenation of the given components.  Empty components, except the 1st one, are removed.
        /// </summary>
        /// <remarks>
        /// If any of the components has <see cref="IsAbsolute"/> true, then it must be the 1st path in the
        /// sequence.  The resulting object will also be absolute.
        /// </remarks>
        /// <param name="pathComponents">Path components to join.</param>
        /// <exception cref="ArgumentException">
        /// If an absolute path occurs anywhere but in the 1st position.
        /// </exception>
        public static PathComponents Join(params PathComponents[] pathComponents) {
            var nonEmptyComponents = pathComponents.Where(x => !x.IsEmpty);
            if (nonEmptyComponents.Skip(1).Any(x => x.IsAbsolute)) {
                var input = string.Join("/", pathComponents.Select(x => x.NormalizedString));
                throw new ArgumentException($"Invalid position of absolute path: {input}");
            }

            var c = nonEmptyComponents.Sum(x => x.components.Length);
            var r = new string[c];
            {
                int o = 0;
                foreach (var pc in nonEmptyComponents) {
                    Array.Copy(pc.components, 0, r, o, pc.components.Length);
                    o += pc.components.Length;
                }
            }
            return new PathComponents(r);
        }

        /// <summary>
        /// Returns new object with the given components prepended to this object.
        /// </summary>
        /// <exception>For any reason that constructor or <see cref="Join(PathComponents[])"/> can throw.</exception>
        readonly public PathComponents Prepend(params string[] components) {
            // TODO: extremely inefficient, but don't want to duplicate validity checking.
            return Join(new PathComponents(components), this);
        }

        /// <summary>
        /// Appends <paramref name="components"/> to the end of this.
        /// </summary>
        readonly public PathComponents Append(params string[] components) {
            // TODO: extremely inefficient, but don't want to duplicate validity checking.
            return Join(this, new PathComponents(components));
        }

        /// <summary>
        /// Check whether this path is a prefix of <paramref name="other"/>.  A path is a prefix of itself.
        /// </summary>
        readonly public bool IsPrefixOf(PathComponents other) {
            return components.SequenceEqual(other.Take(components.Length));
        }

        /// <summary>
        /// Check whether this path  is a suffix of other.  A path is a suffix of itself.
        /// </summary>
        readonly public bool IsSuffixOf(PathComponents other) {
            var d = other.Length - Length;
            if (d < 0) return false;
            return other.components.Skip(d).SequenceEqual(components);
        }

        /// <summary>
        /// Remove a path prefix from this path.
        /// </summary>
        /// <param name="prefix">Prefix to remove.  Must be an actual prefix of this path.</param>
        /// <exception cref="ArgumentOutOfRangeException">The given prefix is not an actual prefix of this path - OR - the
        /// result would be an empty path.</exception>
        readonly public PathComponents RemovePrefix(PathComponents prefix) {
            if (!prefix.IsPrefixOf(this))
                throw new ArgumentOutOfRangeException(nameof(prefix), $"Prefix: {prefix.NormalizedString}, this: {NormalizedString}");
            return RemovePrefix(prefix.Length);
        }

        /// <summary>
        /// Remove the given number of components from this path.
        /// </summary>
        /// <param name="count">Number of path components to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="count"/> is <c>>= this.Lenth</c>.</exception>
        readonly public PathComponents RemovePrefix(int count) {
            if (count >= Length)
                throw new ArgumentOutOfRangeException(nameof(count), $"Attempt to prefix remove {count} from {NormalizedString} with length {Length}.");
            return new PathComponents(components.Skip(count).ToArray());
        }

        /// <summary>
        /// Remove a path suffix from this path
        /// </summary>
        /// <param name="suffix">Suffix to remove.  Must be an actual suffix of this path.</param>
        /// <exception cref="ArgumentOutOfRangeException">The given suffix is not an actual suffix of this path.</exception>
        readonly public PathComponents RemoveSuffix(PathComponents suffix) {
            if (!suffix.IsSuffixOf(this))
                throw new ArgumentOutOfRangeException(nameof(suffix), $"Suffix: {suffix.NormalizedString}, this: {NormalizedString}");
            return RemoveSuffix(suffix.Length);
        }

        /// <summary>
        /// Remove the given number of components from this path.
        /// </summary>
        /// <param name="count">Number of components to remove.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is <c>>= this.Length</c>.</exception>
        readonly public PathComponents RemoveSuffix(int count) {
            if (count >= Length)
                throw new ArgumentOutOfRangeException(nameof(count), $"Attempt to prefix RemoveSuffix {count} from {NormalizedString} with length {Length}.");
            return new PathComponents(components.Take(Length - count).ToArray());
        }

        /// <summary>
        /// Replaces the component at componentIndex with newValue
        /// </summary>
        /// <param name="componentIndex">
        /// Index of component to replace.  May be negative, in which case the components are counted from the end
        /// (e.g., -1 is the last component).
        /// </param>
        /// <param name="newValue">New value of component</param>
        /// <returns></returns>
        readonly public PathComponents ReplaceComponent(int componentIndex, string newValue) {
            if (componentIndex < 0)
                componentIndex += Length;
            if(componentIndex < 0 || componentIndex >= Length)
                throw new ArgumentOutOfRangeException(nameof(componentIndex), $"{NormalizedString}, {componentIndex}");
            var tmp = new string[Length];
            Array.Copy(components, tmp, components.Length);
            tmp[componentIndex] = newValue;
            return new PathComponents(tmp);
        }

#region IEnumerable / array-like support

        /// <summary>
        /// Number of components in this path.
        /// </summary>
        readonly public int Length => components.Length;

        /// <summary>
        /// Indexer.
        /// </summary>
        /// <param name="i">
        /// Component index to get.  Negative values are supported; -1 corresponds to the last component.
        /// </param>
        /// <returns>The i'th path component.</returns>
        readonly public string this[int i] => components[i >= 0 ? i : i + components.Length ];

        /// <inheritdoc/>
        public IEnumerator<string> GetEnumerator() {
            return ((IEnumerable<string>)components).GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() {
            return components.GetEnumerator();
        }

#endregion

#region Equality


        /// <summary>
        /// Two instances are equal if they are component-wise equal AND both have the same value of <see cref="IsAbsolute"/> property.
        /// </summary>
        /// <param name="other">The other instance to compare with.</param>
        /// <returns>True if <paramref name="other"/> is equal to this.</returns>
        public bool Equals(PathComponents other) {
            bool eq = IsAbsolute == other.IsAbsolute && components.SequenceEqual(other.components);
#if DEBUG
            if (eq != (NormalizedString == other.NormalizedString))
                throw new ArgumentException("Inconsistent equality detected.");
#endif
            return eq;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) {
            if (!(obj is PathComponents other)) return false;
            return Equals(other);
        }

        /// <inheritdoc/>
        public override int GetHashCode() {
            int h = IsAbsolute ? 0x31415926 : 0x27182818;
            return h ^ NormalizedString.GetHashCode();
        }

        /// <summary>
        /// Implemented using <see cref="Equals(PathComponents)"/>.
        /// </summary>
        public static bool operator==(PathComponents l, PathComponents r) {
            return l.Equals(r);
        }

        /// <summary>
        /// Implemented using <see cref="Equals(PathComponents)"/>.
        /// </summary>
        public static bool operator!=(PathComponents l, PathComponents r) {
            return !l.Equals(r);
        }

#endregion

    }
}
