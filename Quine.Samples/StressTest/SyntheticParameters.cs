using System;
using System.Text;

namespace Quine.Samples.StressTest;

internal record class SyntheticParameters
{
    /// <summary>
    /// Parameters used to simulate latency and delays; values are drawn from a uniform distribution.
    /// </summary>
    public readonly struct LatencyDistribution
    {
        public readonly int Low;
        public readonly int High;

        /// <summary>
        /// Constructor.  Ensures "sane" ranges for low and high.
        /// </summary>
        /// <param name="low">Low bracket for the random number in milliseconds.</param>
        /// <param name="high">High bracket for the random number in milliseconds.  Must be at least 2 higher than <paramref name="low"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public LatencyDistribution(int low, int high)
        {
            if (low < 1 || high < 1 || high - low < 2)
                throw new ArgumentException("Invalid distribution parameters.");
            Low = low;
            High = high;
        }
    }

    /// <summary>
    /// Total length in bytes to generate / consume.
    /// </summary>
    public int DataLength { get; set; }

    /// <summary>
    /// Passed to <see cref="TransferStateMachine.MaxConcurrency"/>.
    /// </summary>
    public int MaxConcurrency { get; set; }

    public const int InitializeBlock = -1;
    public const int FinalizeBlock = -2;

    /// <summary>
    /// If set to true, data corruption will be introduced at the last byte.
    /// </summary>
    public bool SimulateCorruption { get; set; }

    /// <summary>
    /// If set, <c>IOException</c> will be thrown in the given phase.  Use 'I' for initialize, 'L' for loop, 'F' for finalize.
    /// </summary>
    public char? SimulateIOError { get; set; }

    /// <summary>
    /// If not <c>default</c>, introduces delay to simulate latency.  The delay is also a cancellation point.
    /// </summary>
    public LatencyDistribution? SimulateLatency { get; set; }

    public override string ToString() {
        var sb = new StringBuilder(128);
        sb.Append('(');
        sb.Append(MaxConcurrency);
        if (SimulateCorruption)
            sb.AppendFormat(",H");
        if (SimulateIOError.HasValue)
            sb.AppendFormat(",I/{0}", SimulateIOError.Value);
        if (SimulateLatency.HasValue)
            sb.AppendFormat(",L{0}-{1}", SimulateLatency.Value.Low, SimulateLatency.Value.High);
        sb.Append(')');
        return sb.ToString();
    }

    public static SyntheticParameters SetCorruption(SyntheticParameters self, bool x) => self with { SimulateCorruption = x };
    public static SyntheticParameters SetIOError(SyntheticParameters self, char? x) => self with { SimulateIOError = x };
    public static SyntheticParameters SetLatency(SyntheticParameters self, LatencyDistribution? x) => self with { SimulateLatency = x };
}
