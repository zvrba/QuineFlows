using System;
using System.Collections.Generic;
using System.Linq;
using Quine.FileTransfer;

namespace Quine.Samples.StressTest;

internal class FixtureConfiguration
{
    public const int MaxDataSize = 256 << 20;
    required public SyntheticGenerator.Producer Producer;
    required public SyntheticGenerator.Consumer[] Consumers;
    public Func<ITransferHasher>? HasherFactory;
    public IReadOnlyList<SyntheticGenerator> Workers =>
        _Workers ??= Enumerable.Repeat((SyntheticGenerator)Producer, 1).Concat(Consumers).ToArray();
    private IReadOnlyList<SyntheticGenerator> _Workers = default!;

    public IEnumerable<FixtureConfiguration> Apply<T>
        (
        Func<SyntheticParameters, T, SyntheticParameters> setter,
        IEnumerable<T> values,
        int i
        )
    {
        if (i >= Workers.Count) {
            yield return this;
        }
        else {
            var original = Workers[i].Parameters;
            foreach (var v in values) {
                Workers[i].Parameters = setter(Workers[i].Parameters, v);
                foreach (var _ in Apply(setter, values, i + 1))
                    yield return this;
            }
            Workers[i].Parameters = original;
        }
    }

    // DataLength must be equal for all workers.
    public void SetDataLength(int dl) {
        foreach (var w in Workers)
            w.Parameters.DataLength = dl;
    }
}

