using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Quine.Samples;

internal class SyntheticParameters
{
    /// <summary>
    /// Parameters used to simulate latency and delays; values are drawn from a uniform distribution.
    /// </summary>
    public readonly struct Distribution
    {
        public readonly int Low;
        public readonly int High;

        /// <summary>
        /// True if this is a default instance, i.e., no delay will be simulated.
        /// </summary>
        public bool IsDefault => Low == 0 && High == 0;

        /// <summary>
        /// Constructor.  Ensures "sane" ranges for low and high.
        /// </summary>
        /// <param name="low">Low bracket for the random number in milliseconds.</param>
        /// <param name="high">High bracket for the random number in milliseconds.  Must be at least 2 higher than <paramref name="low"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public Distribution(int low, int high) {
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
    /// If set to a positive value, data corruption will be introduced at the given block number.  Default is -1 (off).
    /// </summary>
    public int? SimulateCorruption { get; set; }

    /// <summary>
    /// If set, <c>IOException</c> will be thrown at the given block number.  Two values are special: <see cref="InitializeBlock"/>
    /// will cause initialization to fail, whereas <see cref="FailBlock"> will cause finalization to fail.
    /// </summary>
    public int? SimulateIOError { get; set; }

    /// <summary>
    /// If not <c>default</c>, introduces delay to simulate latency.  The delay is also a cancellation point.
    /// </summary>
    public Distribution SimulateLatency { get; set; }

    /// <summary>
    /// If set, a delay is introduced to simulates the given value; unit is kByte/s.
    /// </summary>
    public int? SimulateBandwidth { get; set; }

    private readonly StringBuilder sb = new();
    public override string ToString() {
        sb.Clear();
        sb.Append('(');
        sb.Append(MaxConcurrency);
        if (SimulateCorruption.HasValue)
            sb.AppendFormat(",H/{0}", SimulateCorruption.Value);
        if (SimulateIOError.HasValue)
            sb.AppendFormat(",I/{0}", SimulateIOError.Value);
        if (!SimulateLatency.IsDefault)
            sb.AppendFormat(",L{0}-{1}", SimulateLatency.Low, SimulateLatency.High);
        if (SimulateBandwidth.HasValue)
            sb.AppendFormat(",B{0}", SimulateBandwidth.Value);
        sb.Append(')');
        return sb.ToString();
    }

    /// <summary>
    /// Externally provided CTS to simulate cancellation.
    /// </summary>
    public CancellationTokenSource? Cts { get; set; }

    public async Task DelayAsync(int byteCount) {
        var ms = 0;
        if (!SimulateLatency.IsDefault)
            ms = Random.Shared.Next(SimulateLatency.Low, SimulateLatency.High);
        if (SimulateBandwidth.HasValue)
            ms += (int)(1000.0f * byteCount / 1024.0f / SimulateBandwidth.Value);
        if (ms > 0)
            await Task.Delay(ms);
    }

    public void InjectIOException(int sequence) {
        if (sequence == SimulateIOError)
            throw new IOException($"Simulated IO error at sequence#{sequence}.");
    }
}
