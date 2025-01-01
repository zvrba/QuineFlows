using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Quine.HRCatalog;

namespace Quine.Graph;

/// <summary>
/// <para>
/// Limits the number of concurrently running nodes of the same type.
/// Various kinds of limits (per-graph, per-process, etc.) can be achieved by considering the scope and lifetime of the DI
/// registrations.
/// </para>
/// <para>
/// This type is not for public consumption, but it must be registered with DI.
/// </para>
/// </summary>
public sealed class NodeConcurrencyLimiter
{
    private readonly Dictionary<Type, SemaphoreEntry> semaphores = new();

    internal async Task WaitAsync(NodeShell requester) {
        SemaphoreEntry sementry;
        lock (semaphores) {
            if (!semaphores.TryGetValue(requester.GetType(), out sementry)) {
                sementry = new(requester.ConcurrencyLimit);
                semaphores.Add(requester.GetType(), sementry);
            }
            QHEnsure.State(sementry.ConcurrencyLimit == requester.ConcurrencyLimit);
        }
        if (sementry.Semaphore != null)
            await sementry.Semaphore.WaitAsync();
    }

    internal void Release(NodeShell requester) {
        lock (semaphores)
            semaphores[requester.GetType()].Semaphore?.Release();
    }

    // Needed because SemaphoreSlim does not expose MaxCount property publicly.
    private readonly struct SemaphoreEntry {
        public readonly SemaphoreSlim Semaphore;
        public readonly int ConcurrencyLimit;

        public SemaphoreEntry(int concurrencyLimit) {
            ConcurrencyLimit = concurrencyLimit;
            Semaphore = concurrencyLimit == int.MaxValue ? null : new(ConcurrencyLimit, ConcurrencyLimit);
        }
    }
}
