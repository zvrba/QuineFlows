using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quine.FileTransfer;

/// <summary>
/// Data shared between <see cref="ITransferWorker"/> and <see cref="TransferDriver"/> that is used to manage execution.
/// This class cannot be derived from outside of this assembly, but its public properties can be used to inspect
/// the worker's status after execution.
/// </summary>
public abstract class TransferStateMachine
{
    private protected TransferStateMachine(TransferDriver driver, ITransferWorker worker)
    {
        Driver = driver;
        Worker = worker;
        Worker.State = this;
        if (Worker.MaxConcurrency < 1)
            throw new NotImplementedException("MaxConcurrency must be at least 1.");
    }

    internal TransferDriver Driver { get; }
    internal ITransferWorker Worker { get; }
    internal Task Completion = null!;

    /// <summary>
    /// Reflects any errors which occurred during execution (<c>null</c> on successful completion).
    /// This property may be accessed only after the execution has completed.
    /// </summary>
    /// <remarks>
    /// In rare cases, due to inherent race conditions during parallel execution, this property might not reflect the "real"
    /// exception that occurred.  This happens when:
    /// <list type="bullet">
    /// <item>
    /// Both the producer and ALL consumers fail.  The producer's exception will either be its "real" exception or
    /// <see cref="OperationCanceledException"/>.
    /// </item>
    /// <item>
    /// When the producer fails in <see cref="ITransferWorker.FinalizeAsync(ITransferHasher?, ITransferBuffer)"/> (a dubious
    /// failure because it will aready have produced all data there was), "fast" consumers will complete successfully, while
    /// the "slow" ones will complete with <see cref="OperationCanceledException"/>.
    /// </item>
    /// </list>
    /// </remarks>
    /// <exception cref="InvalidOperationException">The property is accessed during execution.</exception>
    public Exception? Exception {
        get {
            if (!Completion.IsCompleted)
                throw new InvalidOperationException($"{nameof(Exception)} can be accessed only after the execution has completed.");
            if (_Exceptions is null)
                return null;
            _Exception ??= CreateException();
            return _Exception;

            Exception CreateException() {
                Debug.Assert(_Exceptions is { Count: > 0 });
                var firstexn = _Exceptions[0];
                _Exceptions.RemoveAll(p => p is OperationCanceledException);
                return _Exceptions.Count switch {
                    // All exceptions were OCE, so just return the first one.
                    0 => firstexn,
                    // firstexn might be OCE, return the remaining one.
                    1 => _Exceptions[0],
                    // Multiple exceptions, return all that are not OCE
                    _ => new AggregateException("Multiple errors occurred.  See InnerExceptions.", _Exceptions),
                };
            }
        } 
    }
    private Exception? _Exception;
    private List<Exception>? _Exceptions;


    /// <summary>
    /// True if execution has failed due to any exception.
    /// </summary>
    public bool IsFaulted {
        get {
            lock (this)
                return _Exceptions is { Count: > 0 };
        }
    }

    /// <summary>
    /// Size of a single block that is transferred from the producer to the consumers.
    /// This is the same value as passed to <see cref="TransferDriver"/> ctor.
    /// Every block, except the last, must be completely filled with data.
    /// </summary>
    public int BlockSize => Driver.BufferPool.BlockSize;

    /// <summary>
    /// Token that MUST be used by implementations on <see cref="ITransferWorker"/> to check for cancellation.
    /// Use <see cref="CancellationToken.ThrowIfCancellationRequested"/> to check for cancellation.
    /// </summary>
    public CancellationToken CancellationToken => _InternalCancellation.Token;
    private CancellationTokenSource _InternalCancellation = null!;

    private protected abstract Task SingleTaskWork();

    internal async Task RunAsync() {
        _InternalCancellation = CancellationTokenSource.CreateLinkedTokenSource(Driver.GlobalCancellation.Token);
        _Exceptions = null;

        try {
            try {
                await Worker.InitializeAsync();
            }
            catch (Exception e) {
                RecordExceptionAndCancelSelf(e);
            }

            await SpawnTasks();

            var verifyHash =
                Driver.ReferenceHasher is not null &&
                Driver.VerifyHash &&
                Worker != Driver.ReferenceHasher &&   // NB! The hasher cannot wait on its own completion (deadlock).
                !IsFaulted;

            ITransferHasher? hasher = null;
            AlignedBuffer? buffer = null;
            object? referenceHash = null;
            try {
                if (verifyHash) {
                    Debug.Assert(Driver.HasherFactory is not null);
                    Debug.Assert(Driver.ReferenceHasher is not null);
                    try {
                        await Driver.ReferenceHasher.State.Completion;
                        referenceHash = (object?)Driver.ReferenceHasher.State.Exception ?? (object?)Driver.ReferenceHash;
                        Debug.Assert(referenceHash is not null);

                        if (referenceHash is byte[]) {
                            hasher = Driver.HasherFactory();
                            buffer = await Driver.RentAsync(default);   // NB! No CT: Finalize MUST be called!
                        }
                    }
                    catch (Exception e) {
                        referenceHash = e;
                    }
                }

                Debug.Assert((hasher is null) == (buffer is null));
                Debug.Assert(!verifyHash == (referenceHash is null && buffer is null && hasher is null));
                
                var verificationHash = await Worker.FinalizeAsync(hasher, buffer!);
                switch (referenceHash, verificationHash) {
                    case (null, null):
                        break;
                    case (Exception e, _):
                        throw new HashVerificationException("Could not verify hash because computation of reference hash failed.", e);
                    case (byte[] rb,  byte[] vb):
                        if (vb is null || vb.Length < 1)
                            throw new HashVerificationException("Verification hash was empty, but no errors were encountered.", null!);
                        if (!vb.SequenceEqual(rb))
                            throw new HashVerificationException(rb, vb);
                        break;
                    default:
                        throw new NotImplementedException("BUG: Invalid data encountered during hash verification.");
                }
            }
            catch (Exception e) {
                RecordExceptionAndCancelSelf(e);
            }
            finally {
                if (buffer is not null)
                    Driver.Return(buffer);
                if (hasher is IDisposable d)
                    d.Dispose();
            }
        }
        finally {
            _InternalCancellation.Dispose();
            _InternalCancellation = null!;

        }
    }

    private protected virtual async Task SpawnTasks() {
        if (Worker.MaxConcurrency == 1) {
            await SingleTaskWork();
        }
        else {
            var tasks = new Task[Worker.MaxConcurrency];
            for (int i = 0; i < Worker.MaxConcurrency; ++i)
                tasks[i] = SingleTaskWork();
            await Task.WhenAll(tasks);
        }
    }

    private protected void RecordExceptionAndCancelSelf(Exception exn) {
        Trace.Assert(exn is not null);
        lock (this) {
            _Exceptions ??= new(4);
            _Exceptions.Add(exn);

            if(_Exceptions.Count == 1) {
                Driver.Fail(this);
                _InternalCancellation.Cancel();
            }
        }

    }
}
