using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Quine.FileTransfer;

/// <summary>
/// Provides methods for creating unbuffered file streams from paths directly accessible to OS.
/// </summary>
public interface IFileStreamOpenStrategy
{
    /// <summary>
    /// Default instance: supports Windows and OSX.
    /// </summary>
    public static IFileStreamOpenStrategy Default => _Default;
    private static IFileStreamOpenStrategy _Default = UnbufferedStreamOpenStrategy.CreateInstance();

    /// <summary>
    /// Allows replacing <see cref="Default"/>.  This is intended to support other operating systems.
    /// </summary>
    /// <param name="newDefault">The new default instance.</param>
    public static void SetDefault(IFileStreamOpenStrategy newDefault) {
        ArgumentNullException.ThrowIfNull(newDefault);
        _Default = newDefault;
    }

    /// <summary>
    /// Opens a file for unbuffered reading.  Must throw exception on failure.
    /// </summary>
    /// <param name="filePath">Path to the file to open.</param>
    /// <returns>A valid <see cref="FileStream"/> instance.</returns>
    FileStream OpenRead(string filePath);

    /// <summary>
    /// Opens and truncates a file for unbuffered writing and reading, overwriting previously existing file.
    /// Must throw exception on failure.  (Read mode is necessary for optional hash verification.)
    /// </summary>
    /// <param name="filePath">Path to the file to open.</param>
    /// <returns>A valid <see cref="FileStream"/> instance.</returns>
    FileStream OpenWrite(string filePath);
}

// Windows: https://learn.microsoft.com/en-us/windows/win32/fileio/file-buffering
// OSX: https://saplin.blogspot.com/2018/07/non-cachedunbuffered-file-operations.html
// OSX: https://github.com/nneonneo/osx-10.9-opensource/blob/master/xnu-2422.1.72/bsd/sys/fcntl.h
// OSX: https://developer.apple.com/library/archive/documentation/System/Conceptual/ManPages_iPhoneOS/man2/fcntl.2.html

/// <summary>
/// Contains predefined OS-specific implementations of <see cref="IFileStreamOpenStrategy"/> that opens unbuffered streams.
/// </summary>
internal static class UnbufferedStreamOpenStrategy
{
    internal static IFileStreamOpenStrategy CreateInstance() {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsStrategy();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new OSXStrategy();
        throw new NotSupportedException($"{Environment.OSVersion} is not supported.");
    }

    private static void RequirePlatform(OSPlatform platform) {
        if (!RuntimeInformation.IsOSPlatform(platform))
            throw new InvalidOperationException($"This implementation can only be used on {platform} OS.");
    }

    /// <summary>
    /// Implmements unbuffered <see cref="IFileStreamOpenStrategy"/> for Windows OS.
    /// </summary>
    private sealed class WindowsStrategy : IFileStreamOpenStrategy
    {
        public WindowsStrategy() => RequirePlatform(OSPlatform.Windows);

        const int F_NOBUFFER = 0x20000000;  // FILE_FLAG_NO_BUFFERING

        // Note: we don't use write through.  See these links:
        // https://devblogs.microsoft.com/oldnewthing/20210729-00/?p=105494
        // https://devblogs.microsoft.com/oldnewthing/20140306-00/?p=1583

        /// <inheritdoc/>
        public FileStream OpenRead(string filePath) => new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.None, 0,
            FileOptions.Asynchronous | FileOptions.SequentialScan | (FileOptions)F_NOBUFFER);


        /// <inheritdoc/>
        public FileStream OpenWrite(string filePath) => new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 0,
            FileOptions.Asynchronous | FileOptions.SequentialScan | (FileOptions)F_NOBUFFER);
    }

    /// <summary>
    /// Implements unbuffered <see cref="IFileStreamOpenStrategy"/> for OSX.
    /// </summary>
    private sealed class OSXStrategy : IFileStreamOpenStrategy
    {
        public OSXStrategy() => RequirePlatform(OSPlatform.OSX);

        const int F_NOBUFFER = 48;          // F_NOCACHE

        /// <inheritdoc/>
        public FileStream OpenRead(string filePath) {
            var ret = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
            Fcntl((int)ret.SafeFileHandle.DangerousGetHandle(), F_NOBUFFER, 1);
            return ret;
        }

        /// <inheritdoc/>
        public FileStream OpenWrite(string filePath) {
            var ret = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20,
                FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.WriteThrough);
            Fcntl((int)ret.SafeFileHandle.DangerousGetHandle(), F_NOBUFFER, 1);
            return ret;
        }

        private static void Fcntl(int fd, int op, int data) {
            if (fcntl(fd, op, data) < 0)
                throw new NotSupportedException($"Fcntl failed with errno={Marshal.GetLastPInvokeError()}.");
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int fcntl(int fd, int op, int data);
    }

}
