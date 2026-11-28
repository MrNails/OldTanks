using System.Runtime.CompilerServices;

namespace CoolEngine.Core.Channels;

/// <summary>
/// A multi-producer, single-consumer command queue whose sequence numbers make an acknowledgement exact: the
/// sequence and the position are assigned in one critical section, so queue order equals sequence order and every
/// batch is a contiguous sequence range starting after the previous one. A consumer that reports
/// <c>LastSequence = N</c> as applied has taken and applied every command with a sequence ≤ N.
/// </summary>
/// <remarks>
/// A locked double buffer: producers write only the array <c>m_pending</c> points to, under the lock; the consumer
/// swaps the two arrays under the lock and reads its batch without it. The lock's release by a producer and its
/// acquisition by the consumer's swap order every producer write before the consumer's reads. Unbounded: the pending
/// array doubles on a burst and is kept.
/// </remarks>
public sealed class CommandQueue<T> where T : struct
{
    private readonly Lock m_lock = new();

    // Guarded by m_lock: written by producers in Enqueue and by the consumer's swap in TakePending.
    private T[] m_pending;
    private int m_pendingCount;
    private long m_lastAssigned;

    // Owned by the consumer thread; producers never touch the array m_taken points to.
    private T[] m_taken;
    private int m_takenCount;
    private long m_lastTaken;

    /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCapacity"/> below 1.</exception>
    public CommandQueue(int initialCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(initialCapacity, 1);

        m_pending = new T[initialCapacity];
        m_taken = new T[initialCapacity];
    }

    /// <summary>
    /// The consumer thread: the <see cref="CommandBatch{T}.LastSequence"/> of the latest batch (0 before any).
    /// </summary>
    public long LastTakenSequence => m_lastTaken;

    /// <summary>
    /// Any thread. Copies the command in and returns its sequence (1, 2, …). Allocates only when the pending array
    /// doubles.
    /// </summary>
    /// <exception cref="OutOfMemoryException">The pending array cannot grow.</exception>
    public long Enqueue(in T command)
    {
        // The lock covers one store and two counters; no user code runs under it.
        lock (m_lock)
        {
            if (m_pendingCount == m_pending.Length)
                GrowPending();

            m_pending[m_pendingCount++] = command;
            return ++m_lastAssigned;
        }
    }

    /// <summary>
    /// The one consumer thread. Takes every command enqueued so far, in sequence order. Allocates nothing. The batch
    /// is valid until the next call.
    /// </summary>
    public CommandBatch<T> TakePending()
    {
        // The previous batch's slots are the consumer's until the swap below hands the array to the producers:
        // clear them first, so the queue keeps no reference alive.
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(m_taken, 0, m_takenCount);

        long lastAssigned;
        lock (m_lock)
        {
            (m_pending, m_taken) = (m_taken, m_pending);
            m_takenCount = m_pendingCount;
            m_pendingCount = 0;
            lastAssigned = m_lastAssigned;
        }

        var firstSequence = m_lastTaken + 1;
        m_lastTaken = lastAssigned;
        return new CommandBatch<T>(new ReadOnlySpan<T>(m_taken, 0, m_takenCount), firstSequence, lastAssigned);
    }

    // Under m_lock.
    private void GrowPending()
    {
        var length = m_pending.Length;
        var newLength = (int)Math.Min(2L * length, Array.MaxLength);
        if (newLength == length)
            throw new OutOfMemoryException("The command queue cannot grow beyond Array.MaxLength commands.");

        var grown = new T[newLength];
        Array.Copy(m_pending, grown, m_pendingCount);
        m_pending = grown;
    }
}
