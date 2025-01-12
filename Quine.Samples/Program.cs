using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Quine.Samples.StressTest;

namespace Quine.Samples;

internal class Program
{
    static async Task<int> Main(string[] args) {
        var rootCmd = new RootCommand(typeof(Program).Assembly.GetName().Name!);
        
        var stressCmd = new Command("StressTest", "Run a stress-test.  WARNING: This takes a LONG time.");
        stressCmd.SetHandler(Runner.ExecuteAsync);
        rootCmd.AddCommand(stressCmd);

        var copyCmd = new Command("CopyDir", "Non-recursive copy of files in directory.");
        var srcOpt = new Argument<DirectoryInfo>("source", "Source directory") { Arity = ArgumentArity.ExactlyOne };
        var dstOpt = new Argument<DirectoryInfo[]>("destinations", "Destination directory") { Arity = ArgumentArity.OneOrMore };
        copyCmd.AddArgument(srcOpt);
        copyCmd.AddArgument(dstOpt);
        copyCmd.SetHandler(CopyDirectory.ExecuteAsync, srcOpt, dstOpt);
        rootCmd.AddCommand(copyCmd);

        var watchCmd = new Command("WatchDir", "Demo for watch folders");
        watchCmd.AddArgument(srcOpt);
        watchCmd.SetHandler(WatchDirectory.ExecuteAsync, srcOpt);
        rootCmd.AddCommand(watchCmd);

        var graphCmd = new Command("GraphSample", "Graph framework sample");
        var countOpt = new Argument<int>("count", "Count of items to generate") { Arity = ArgumentArity.ExactlyOne };
        var incOpt = new Argument<int>("increment", "Increment at source") { Arity = ArgumentArity.ExactlyOne };
        var constOpt = new Argument<int>("constant", "Constant to subtract.") { Arity = ArgumentArity.ExactlyOne };
        graphCmd.AddArgument(countOpt);
        graphCmd.AddArgument(incOpt);
        graphCmd.AddArgument(constOpt);
        graphCmd.SetHandler(GraphSample.ExecuteAsync, incOpt, countOpt, constOpt);
        rootCmd.AddCommand(graphCmd);

        return await rootCmd.InvokeAsync(args);
    }
}
