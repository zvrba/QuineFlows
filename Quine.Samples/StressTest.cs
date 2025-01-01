using Quine.FileTransfer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Quine.Samples;

internal class StressTest
{
    // Small buffer to test concurrency.  Total of 1MB.
    private const int BlockSize = 16384;
    private static readonly TransferDriver Driver = new(BlockSize, 64);
    private static readonly SyntheticGenerator.Producer Producer = new() { Parameters = new() };
    private static readonly SyntheticGenerator.Consumer[] Consumers = Enumerable.Range(0, 3)
        .Select(x => new SyntheticGenerator.Consumer() { Parameters = new() }).ToArray();
    private static readonly Func<ITransferHasher> HasherFactory = () => new XX64TransferHash();
    private static readonly Fixture Fixture = new(Driver);


    public static async Task ExecuteAsync() {
        Console.WriteLine("STARTING TEST.");

        foreach (var fc in GenerateTopology())
        foreach (var _0 in GenerateConcurrency(fc))
        foreach (var _1 in GenerateDataLengths(fc)) {
            // We do NOT need to simulate data corruption and IO error simultaneously: IO error will prevent hash verification.
            
            foreach (var _ in GenerateDataCorruption(fc))
                await Fixture.ExecuteAsync(Driver, fc);

            foreach (var _3 in GenerateIOErrors(fc, -1))
                await Fixture.ExecuteAsync(Driver, fc);
        }
        Console.WriteLine("DONE.");
    }

    static IEnumerable<FixtureConfiguration> GenerateTopology() {
        yield return new() { Producer = Producer, Consumers = [Consumers[0]], HasherFactory = null };
        yield return new() { Producer = Producer, Consumers = [Consumers[0]], HasherFactory = HasherFactory, };
        yield return new() { Producer = Producer, Consumers = Consumers, HasherFactory = HasherFactory, };
    }

    const int ConcurrencyLevel = 6; // The machine has 12 cores and 20 logical processors.

    static IEnumerable<FixtureConfiguration> GenerateConcurrency(FixtureConfiguration c) {
        for (var ps = 0; ps < 2; ++ps) {    // Serial or parallel producer.
            c.Producer.Parameters.MaxConcurrency = ps == 0 ? 1 : ConcurrencyLevel;

            // All consumers serial
            foreach (var x in c.Consumers) x.Parameters.MaxConcurrency = 1;
            yield return c;

            // One consumer parallel.
            c.Consumers[0].Parameters.MaxConcurrency = ConcurrencyLevel;
            yield return c;

            if (c.Consumers.Length > 1) {
                // One consumer serial.
                c.Consumers[1].Parameters.MaxConcurrency = ConcurrencyLevel;
                yield return c;

                // All consumers parallel.
                c.Consumers[2].Parameters.MaxConcurrency = ConcurrencyLevel;
                yield return c;
            }
        }
    }

    private static readonly int[] DataLengths = {
        0, 1,
        BlockSize - 1, BlockSize, BlockSize + 1,
        256 * BlockSize - 1, 256 * BlockSize, 256 * BlockSize + 1,
        FixtureConfiguration.MaxDataSize - 1, FixtureConfiguration.MaxDataSize
    };

    static IEnumerable<FixtureConfiguration> GenerateDataLengths(FixtureConfiguration c) {
        foreach (var dl in DataLengths) {
            c.Producer.Parameters.DataLength = dl;

            for (var i = 0; i < c.Consumers.Length; ++i)
                c.Consumers[i].Parameters.DataLength = dl;

            yield return c;
        }
    }

    static IEnumerable<FixtureConfiguration> GenerateDataCorruption(FixtureConfiguration c) {
        if (c.Producer.Parameters.DataLength == 0 || c.HasherFactory is null) {
            SetCorruptedBlock(c.Producer.Parameters, null);
            foreach (var x in c.Consumers) 
                SetCorruptedBlock(x.Parameters, null);
            yield return c;
            yield break;
        }

        var block = c.Producer.Parameters.DataLength / BlockSize / 2;
        for (var p = 0; p < 2; ++p) {
            SetCorruptedBlock(c.Producer.Parameters, p == 0 ? null : block);
            for (var i = 0; i < (1 << c.Consumers.Length); ++i)
                for (var j = 0; j < c.Consumers.Length; ++j) 
                    SetCorruptedBlock(c.Consumers[j].Parameters, (i & (1 << j)) == 0 ? null : block);

            yield return c;
        }

        static void SetCorruptedBlock(SyntheticParameters p, int? block) {
            p.SimulateCorruption = block;
            p.SimulateIOError = null;
        }
    }

    private static readonly int?[] ExnBlocks = [null, -1, -2, 0];

    static IEnumerable<FixtureConfiguration> GenerateIOErrors(FixtureConfiguration c, int i) {
        if (i >= c.Consumers.Length || c.Producer.Parameters.DataLength == 0) {
            yield return c;
        } else {
            var p = i == -1 ? c.Producer.Parameters : c.Consumers[i].Parameters;
            foreach (var e in ExnBlocks) {
                p.SimulateIOError = e;
                p.SimulateCorruption = null;
                foreach (var _ in GenerateIOErrors(c, i + 1))
                    yield return c;
            }
        }
    }
}
