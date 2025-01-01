using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quine.FileWatcher;
using Quine.Schemas.Core;

namespace Quine.Samples;

internal class WatchDirectory : IErrorContext
{
    public static async Task ExecuteAsync(DirectoryInfo src) {
        Console.WriteLine("Watched directory: " + src.FullName);
        Console.WriteLine("Press Q to exit.");

        var instance = new WatchDirectory(src);
        var t = instance.PollAsync();
        instance.WaitForKeyboard();
        await t;
    }

    private WatchDirectory(DirectoryInfo src) {
        #region Abstract tree

        // First we create an "abstract" hierarchy.
        var root = WatchNode.MakeRoot();
        var c1 = root.MakeChild(PathComponents.Make("A"));
        var c2 = c1.MakeChild(PathComponents.Make("$(DayNumber)"), GetDayNumber);
        var c3 = c2.MakeChild(PathComponents.Make("$(Key)"), null, "Value");
        var c4 = root.MakeChild(PathComponents.Make("B"));
        var c5 = c4.MakeChild(PathComponents.Make("$(OtherKey)"), null, "OtherValue");

        static bool GetDayNumber(string input, out object value) {
            if (int.TryParse(input, out var v)) {
                value = v;
                return true;
            }
            value = null;
            return false;
        }

        #endregion

        #region Instantiation

        // Make the concrete root.  Use our implementation of IErrorContext.
        this.root = WatchNode.Clone(root, PathComponents.Make(src.FullName), this);

        #endregion
    }

    private readonly CancellationTokenSource cts = new();
    private readonly WatchNode root;

    #region Error context

    // Ignore particular exception: wait until the directories are created. 
    Exception IErrorContext.Accept(in Quine.FileWatcher.ErrorInfo errorInfo) =>
        errorInfo.Exception is DirectoryNotFoundException ? null : errorInfo.Exception;

    #endregion

    #region Poll loop

    private async Task PollAsync() {
    loop:
        try {
            var newEntries = WatchNode.Walk(root).NewEntries.ToList();  // NB! Because it's lazily enumerated.

            Console.WriteLine($"\nROUND: {DateTime.Now}: {newEntries.Count} new entries in this round.");
            foreach (var e in newEntries) {
                var ps = string.Join(',', e.Parameters.Select(kv => $"{kv.Key}={kv.Value}"));
                Console.WriteLine($"{e.Path.NormalizedString}: {ps}");         // We're lazy.
            }
            await Task.Delay(TimeSpan.FromSeconds(5), cts.Token);
            goto loop;
        }
        catch (OperationCanceledException) {
            // Done
        }
    }

    #endregion

    private void WaitForKeyboard() {
        while (true) {
            var key = Console.ReadKey();
            if (key.KeyChar == 'q' || key.KeyChar == 'Q') {
                cts.Cancel();
                break;
            }
        }
    }
}
