using System;
using System.Threading.Tasks;

namespace Quine.FileTransfer;

/// <summary>
/// Common properties and methods shared between <see cref="ITransferProducer"/> and <see cref="ITransferConsumer"/>.
/// The implementation of async methods MUST check <see cref="TransferStateMachine.CancellationToken"/> for cancellation.
/// </summary>
public interface ITransferWorker
{
    /// <summary>
    /// Managed by <see cref="TransferDriver"/>.   The implementation should initialize this property to <c>null</c>.
    /// The driver injects a valid instance before execution starts.  After execution, the instance may be inspected
    /// for errors.
    /// </summary>
    TransferStateMachine State { get; set; }

    /// <summary>
    /// Maximum number of concurrent tasks to use during parallel transfers.
    /// For serial transfers, this property must return 1.
    /// </summary>
    int MaxConcurrency { get; }

    /// <summary>
    /// Initializes the worker for the next transfer, acquiring any needed resources.  Failure must be signaled by throwing an exception.
    /// This method is always invoked, once per transfer.
    /// </summary>
    /// <returns>Task.</returns>
    Task InitializeAsync();

    /// <summary>
    /// <para>
    /// Finalizes the worker by computing the verification hash and disposing of any previously acquired resources.
    /// Failure must be signaled by throwing an exception.
    /// This method is always invoked, once per transfer.  It is invoked regardless of whether the worker encountered
    /// an error or not (<see cref="TransferStateMachine.Exception"/>).
    /// </para>
    /// <para>
    /// IMPORTANT: Resources should be disposed even if cancellation is requested.
    /// </para>
    /// </summary>
    /// <param name="transferHasher">
    /// A ready hasher instance to use for hash computation.  If <c>null</c>, hash verification must be skipped.
    /// This parameter is guaranteed to be <c>null</c> if the worker encountered errors previously.
    /// </param>
    /// <param name="buffer">Buffer to use for reading in file data for hashing.</param>
    /// <returns>Verification hash, or <c>null</c> if verification was not requested.</returns>
    Task<byte[]?> FinalizeAsync(ITransferHasher? transferHasher, ITransferBuffer buffer);
}

/// <summary>
/// This interface must be implemented by the producer-side of a transfer.
/// </summary>
public interface ITransferProducer : ITransferWorker
{
    /// <summary>
    /// <para>
    /// Fills a single buffer with data from the file as determined by its <see cref="ITransferBuffer.Memory"/>
    /// and <see cref="ITransferBuffer.Sequence"/> properties.  Failure must be signaled by throwing an exception.
    /// This method will be invoked concurrently when <see cref="ITransferWorker.MaxConcurrency"/> is greater than 1.
    /// The driver stops invoking this method after an error has occurred.
    /// </para>
    /// <para>
    /// WARNING: The buffer MUST be completely filled with data unless this is the last block of the file.
    /// If this rule is not followed, data will be corrupt at the destinations.
    /// </para>
    /// </summary>
    /// <param name="buffer">
    /// Buffer to fill.  The sequence number might indicate a position beyond EOF, in which case the method must retun 0.
    /// </param>
    /// <returns>
    /// Length of the initial portion of <paramref name="buffer"/> that was filled with valid data.
    /// To signal EOF, return 0.
    /// </returns>
    Task<int> FillAsync(ITransferBuffer buffer);
}

/// <summary>
/// This interface must be implemented by the consumer-side of a transfer.
/// </summary>
public interface ITransferConsumer : ITransferWorker
{
    /// <summary>
    /// <para>
    /// Consumes the data in <paramref name="buffer"/> as determined by its <see cref="ITransferBuffer.Data"/>,
    /// and <see cref="ITransferBuffer.Sequence"/> properties.  Failure must be signaled
    /// by throwing an exception.  This method will be invoked concurrently when <see cref="ITransferWorker.MaxConcurrency"/>
    /// is greater than 1.
    /// The driver stops invoking this method after an error has occurred.
    /// </para>
    /// <para>
    /// WARNING: The data from the buffer MUST be completely consumed.
    /// If this rule is not followed, data will be corrupt at the destinations.
    /// </para>
    /// </summary>
    /// <param name="buffer">
    /// Buffer to consume.  <see cref="ITransferBuffer.Memory"/>
    /// </param>
    /// <returns>Task.</returns>
    Task DrainAsync(ITransferBuffer buffer);
}

/// <summary>
/// This interface must be implemented by hash algorithms.  The interface provides s default implementation
/// for <see cref="ICloneable.Clone"/>, which just invokes <see cref="Clone"/>.
/// </summary>
/// <remarks>
/// The library creates new hasher instances when necessary (see <see cref="TransferDriver.HasherFactory"/>).
/// If the instance implements <c>IDisposable</c>, the library will dispose of it when the instance is no
/// longer needed.
/// </remarks>
public interface ITransferHasher : ICloneable
{
    /// <summary>
    /// Clones <c>this</c>.
    /// </summary>
    /// <returns>A fresh instance that performs the same algorithm as <c>this</c>.</returns>
    new ITransferHasher Clone();
    object ICloneable.Clone() => Clone();

    /// <summary>
    /// Appends a chunk of data to the hasher's state.
    /// </summary>
    /// <param name="data">Data to add to the current hash.</param>
    void Append(ReadOnlySpan<byte> data);

    /// <summary>
    /// Finalizes hash computation and prepares the instance for the next data stream.
    /// </summary>
    /// <returns>
    /// The computed hash value.
    /// </returns>
    byte[] GetHashAndReset();
}
