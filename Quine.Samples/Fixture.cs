using Quine.FileTransfer;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quine.Samples;

internal class FixtureConfiguration
{
    public const int MaxDataSize = 256 << 20;
    required public SyntheticGenerator.Producer Producer;
    required public SyntheticGenerator.Consumer[] Consumers;
    public Func<ITransferHasher>? HasherFactory;
}

internal class Fixture
{
    private readonly Stopwatch TimeMeasure = new();
    private readonly TransferDriver driver;

    internal Fixture(TransferDriver driver) => this.driver = driver;


    /// <summary>
    /// Sets up a job.  The 1st parameter is producer, the others are consumers.
    /// </summary>
    public async Task ExecuteAsync(TransferDriver driver, FixtureConfiguration fc) {
        driver.Producer = fc.Producer;
        driver.Consumers = fc.Consumers;
        if (fc.HasherFactory is not null) {
            driver.HasherFactory = fc.HasherFactory;
            driver.VerifyHash = true;
        }
        else {
            driver.HasherFactory = null;
            driver.VerifyHash = false;
        }


        CancellationTokenSource cts = new();
        try {
            fc.Producer.Parameters.Cts = cts;
            foreach (var c in fc.Consumers)
                c.Parameters.Cts = cts;

            TimeMeasure.Restart();
            await driver.ExecuteAsync(default);
            TimeMeasure.Stop();
        }
        finally {
            cts.Dispose();
            cts = null!;
        }

        Console.WriteLine(string.Format("==SIZE: {0}, {1}, TIME: {2}",
            fc.Producer.Parameters.DataLength, fc.HasherFactory is null ? "NOHASH" : "HASH", TimeMeasure.Elapsed));

        Validate("Producer", fc.Producer.State, fc.Producer.Parameters);

        // Producer throwing I/O error at finalization is dubious at most as this happens after having read all data.
        // In this case, fast consumers will have succeeded, while the slow ones will be cancelled which will be
        // reflected in their Exception property.
        if (fc.Producer.Parameters.SimulateCorruption.HasValue || fc.Producer.Parameters.SimulateIOError >= SyntheticParameters.InitializeBlock) {
            if (!fc.Consumers.All(x => x.State.Exception is not null))
                Console.WriteLine("Producer failed, but some consumers succeeded.");
        }
        else {
            for (var i = 0; i < fc.Consumers.Length; ++i)
                Validate($"Consumer_{i}", fc.Consumers[i].State, fc.Consumers[i].Parameters);
        }
    }

    private static void Validate(string name, TransferStateMachine sm, SyntheticParameters p) {
        Console.WriteLine($"{name} {p}");

        if (sm.Exception is null) {
            if (((SyntheticGenerator)sm.Worker).BytesTransferred != p.DataLength)
                Report($"ERROR: {name} did not transfer the correct data amount.");
        }
        else if (p.SimulateIOError.HasValue) {
            // The exception might be ambiguous when both producer and all consumers fail.
            if (sm.Exception is not (IOException or OperationCanceledException))
                Report($"ERROR: {name} did not throw IOException.");
        }
        else if (p.SimulateCorruption.HasValue) {
            if (sm.Exception is not HashVerificationException)
                Report($"ERROR: {name} threw `{sm.Exception?.GetType()?.Name}` instead of HashVerificationException.");
        }
        else if (sm.Exception is not OperationCanceledException) {
            Report($"ERROR: {name} threw unexpected exception {sm.Exception.GetType().Name}: {sm.Exception.Message}");
        }
    }

    private static void Report(string s) {
        Console.WriteLine(s);
        Environment.Exit(1);
    }
}
