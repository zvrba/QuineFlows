using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Quine.FileTransfer;

namespace Quine.Samples.StressTest;

internal class Runner
{
    // Small buffer to test concurrency.  Total of 1MB.
    private const int BlockSize = 16384;
    private static readonly TransferDriver Driver = new(BlockSize, 64);
    private static readonly SyntheticGenerator.Producer Producer = new() { Parameters = new() };
    private static readonly SyntheticGenerator.Consumer[] Consumers = Enumerable.Range(0, 3)
        .Select(x => new SyntheticGenerator.Consumer() { Parameters = new() }).ToArray();
    private static readonly Func<ITransferHasher> HasherFactory = () => new XX64TransferHash();
    private static readonly Fixture Fixture = new(Driver);

    const int ConcurrencyLevel = 6; // The machine has 12 cores and 20 logical processors.

    private static readonly int[] DataLengths = {
        0, 1,
        BlockSize - 1, BlockSize, BlockSize + 1,
        256 * BlockSize - 3141, 256 * BlockSize, 256 * BlockSize + 3141,
        FixtureConfiguration.MaxDataSize - 1, FixtureConfiguration.MaxDataSize
    };

    // Millisecond intervals
    static readonly SyntheticParameters.LatencyDistribution?[] Latencies = {
        new(5, 20), new(40, 60), new(90, 130)
    };

    // Bytes per second: 1MB/s, 10MB/s, 250MB/s
    static readonly int?[] Bandwidths = {
        1000000, 10000000, 250000000
    };

    public static async Task ExecuteAsync()
    {
        Console.WriteLine("STARTING TEST.");
        foreach (var fc in GenerateTopology()) {
            foreach (var _0 in GenerateConcurrency(fc)) {

                // To simulate the effects of delay wrt correct handling of buffers, we use 4MB size.  Anything larger makes
                // the test run "forever" because latency adds up with small blocks of 16k.
                // We also skip the test when hasher is null, since it's the only way to check for errors.
                if (fc.HasherFactory is not null) {
                    fc.SetDataLength(256 * BlockSize);
                    foreach (var _1 in fc.Apply(SyntheticParameters.SetLatency, Latencies, 0))
                        await Fixture.ExecuteAsync(Driver, fc);
                }
                Trace.Assert(fc.Workers.All(x => x.Parameters.SimulateLatency is null));

                // To check handling of IO and hash errors, we use a range of different lengths that have previously caused problems.
                // We do NOT need to simulate data corruption and IO error simultaneously: IO error will prevent hash verification.
                foreach (var dl in DataLengths) {
                    fc.SetDataLength(dl);

                    foreach (var _ in fc.Apply(SyntheticParameters.SetIOError, [(char?)null, 'I', 'L', 'F'], 0))
                        await Fixture.ExecuteAsync(Driver, fc);
        
                    // Cannot simulate corruption without data.
                    if (dl > 0) {
                        if (fc.HasherFactory is not null) { 
                            foreach (var _ in fc.Apply(SyntheticParameters.SetCorruption, [false, true], 0))
                                await Fixture.ExecuteAsync(Driver, fc);
                        }
                    }
                }
            }
        }
        Console.WriteLine("DONE.");
    }

    static IEnumerable<FixtureConfiguration> GenerateTopology()
    {
        yield return new() { Producer = Producer, Consumers = [Consumers[0]], HasherFactory = null };
        yield return new() { Producer = Producer, Consumers = [Consumers[0]], HasherFactory = HasherFactory, };
        yield return new() { Producer = Producer, Consumers = Consumers, HasherFactory = HasherFactory, };
    }

    // These need not be undoable.
    static IEnumerable<FixtureConfiguration> GenerateConcurrency(FixtureConfiguration c)
    {
        for (var ps = 0; ps < 2; ++ps)
        {
            // Serial or parallel producer.
            c.Producer.Parameters.MaxConcurrency = ps == 0 ? 1 : ConcurrencyLevel;

            // All consumers serial
            foreach (var x in c.Consumers) x.Parameters.MaxConcurrency = 1;
            yield return c;

            // One consumer parallel.
            c.Consumers[0].Parameters.MaxConcurrency = ConcurrencyLevel;
            yield return c;

            if (c.Consumers.Length > 1)
            {
                // One consumer serial (0 and 1 are parallel)
                c.Consumers[1].Parameters.MaxConcurrency = ConcurrencyLevel;
                yield return c;

                // All consumers (0, 1 and 2) parallel.
                c.Consumers[2].Parameters.MaxConcurrency = ConcurrencyLevel;
                yield return c;
            }
        }
    }
}
