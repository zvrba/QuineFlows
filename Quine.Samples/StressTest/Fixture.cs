using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Quine.FileTransfer;

namespace Quine.Samples.StressTest;

internal class Fixture
{
    private readonly Stopwatch TimeMeasure = new();
    private readonly TransferDriver driver;

    internal Fixture(TransferDriver driver) => this.driver = driver;


    /// <summary>
    /// Sets up a job.  The 1st parameter is producer, the others are consumers.
    /// </summary>
    public async Task ExecuteAsync(TransferDriver driver, FixtureConfiguration fc)
    {
        driver.Producer = fc.Producer;
        driver.Consumers = fc.Consumers;
        if (fc.HasherFactory is not null)
        {
            driver.HasherFactory = fc.HasherFactory;
            driver.VerifyHash = true;
        }
        else
        {
            driver.HasherFactory = null;
            driver.VerifyHash = false;
        }

        TimeMeasure.Restart();
        await driver.ExecuteAsync(default);
        TimeMeasure.Stop();

        Console.WriteLine(string.Format("==SIZE: {0}, {1}, TIME: {2}",
            fc.Producer.Parameters.DataLength, fc.HasherFactory is null ? "NOHASH" : "HASH", TimeMeasure.Elapsed));

        // If producer has failed, the copy operation MUST be considered as failed.
        // However, producer's failure does not necessarily force consumers to fail with an excaption; an example is
        // producer failing in FinalizeAsync(), where hash verificaiton is baked in.  If the producer's FinalizeAsync()
        // fails, "fast" consumers will have succeeded, while the slow ones will fail with OCE.
        Validate("Producer", fc.Producer.State, fc.Producer.Parameters);
        for (var i = 0; i < fc.Consumers.Length; ++i)
            Validate($"Consumer_{i}", fc.Consumers[i].State, fc.Consumers[i].Parameters);
    }

    private static void Validate(string name, TransferStateMachine sm, SyntheticParameters p)
    {
        Console.WriteLine($"{name} {p}");

        if (sm.Exception is null)
        {
            if (((SyntheticGenerator)sm.Worker).BytesTransferred != p.DataLength)
                Report($"ERROR: {name} did not transfer the correct data amount.");
        }
        else if (p.SimulateIOError.HasValue)
        {
            // The exception might be ambiguous when both producer and all consumers fail.
            if (sm.Exception is not (IOException or OperationCanceledException))
                Report($"ERROR: {name} did not throw IOException.");
        }
        else if (p.SimulateCorruption)
        {
            if (sm.Exception is not HashVerificationException)
                Report($"ERROR: {name} threw `{sm.Exception?.GetType()?.Name}` instead of HashVerificationException.");
        }
        else if (sm.Exception is not OperationCanceledException)
        {
            Report($"ERROR: {name} threw unexpected exception {sm.Exception.GetType().Name}: {sm.Exception.Message}");
        }
    }

    private static void Report(string s)
    {
        Console.WriteLine(s);
        Environment.Exit(1);
    }
}
