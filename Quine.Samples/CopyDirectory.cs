using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Quine.FileTransfer;
using Quine.Schemas.Core;

namespace Quine.Samples;

internal class CopyDirectory : IDisposable
{
    #region Entry point
    
    public static async Task ExecuteAsync(DirectoryInfo src, DirectoryInfo[] dst) {
        // Create the "holder" for pool and driver.
        using var copier = new CopyDirectory(dst.Length);

        // Initialize paths.
        copier.srcPath = PathComponents.Make(src.FullName);
        copier.dstPaths = dst.Select(x => PathComponents.Make(x.FullName)).ToArray();

        // It is wise to allow only absolute paths for security reasons: relative paths might open up for overwriting arbitrary files.
        // (A full SMB paths is also considered absolute.)
        if (!copier.srcPath.IsAbsolute || copier.dstPaths.Any(x => !x.IsAbsolute))
            throw new InvalidOperationException("All paths must be absolute.");

        // Use the same instances of driver and workers to copy many files.
        foreach (var file in src.EnumerateFiles()) {
            // IMPORTANT! The driver can copy only a single file at a time.  DO NOT spawn multiple copies in parallel.
            await copier.CopyFile(file.Name);
        }

        // Driver is no longer usable after disposal.
    }

    #endregion

    #region Setup driver

    // Pool and driver
    private readonly TransferDriver driver;

    // Producer and consumers
    private readonly UnbufferedFile.Reader reader;
    private readonly UnbufferedFile.Writer[] writers;

    private CopyDirectory(int dstcount) {
        // 16 blocks of 2MB; should be sufficient to saturate common SSDs.
        driver = new(2 << 20, 16);

        reader = new UnbufferedFile.Reader();
        writers = Enumerable.Range(0, dstcount)
            .Select(x => new UnbufferedFile.Writer())
            .ToArray();

        driver.Producer = reader;
        driver.Consumers = writers;

        // configure hash verification
        driver.HasherFactory = () => new XX64TransferHash();
        driver.VerifyHash = true;
    }

    // Once the pool has been disposed, the driver becomes unusable.
    public void Dispose() => driver.Dispose();

    #endregion

    #region Copy single file

    // Source and destination paths
    private PathComponents srcPath;
    private PathComponents[] dstPaths;

    private async Task CopyFile(string srcFileName) {
        // 1: Set up reader and writers to point to source / destinations.
        // For local files, we just use the file's path.
        reader.FilePath = srcPath.Append(srcFileName).NativeString;
        foreach (var x in writers.Zip(dstPaths))
            x.First.FilePath = x.Second.Append(srcFileName).NativeString;

        // 2: Execute copy.  We don't support cancellation in this program.
        await driver.ExecuteAsync(default);

        // 3: Check for errors and report.
        var anyerror = false;
        if (reader.State.Exception is not null) {
            Console.WriteLine($"ERROR: reading file {reader.FilePath}: {reader.State.Exception.Message}");
            anyerror = true;
        }
        foreach (var w in writers.Where(x => x.State.Exception is not null)) {
            Console.WriteLine($"ERROR: writing file {w.FilePath}: {w.State.Exception!.Message}");
            anyerror = true;
        }
        if (!anyerror)
            Console.WriteLine($"OK: copied {reader.FilePath} to all destinations.");
    }

    #endregion
}
