namespace CoolEngine.Core.Channels;

/// <summary>
/// A single-writer, single-reader triple buffer: the writer fills <see cref="WriteTarget"/> and publishes it, the
/// reader acquires the newest published instance. Both are wait-free (one atomic exchange each) and allocate nothing.
/// </summary>
/// <remarks>
/// <para>{back, middle index, front} is always a permutation of {0, 1, 2}: each exchange swaps one side's own index
/// with the middle atomically and each side changes only its own index, so the writer never writes the instance the
/// reader holds.</para>
/// <para>No torn snapshot: the writer's stores into its instance precede its <c>Interlocked.Exchange</c> (a full
/// fence); the reader reaches that instance only through its own <c>Interlocked.Exchange</c> (a full fence), which
/// read the writer's value, and the writer touches the instance again only after the reader has handed it back
/// through another exchange.</para>
/// <para>Never older than seen: the <c>Fresh</c> bit is set only by a publish and cleared only by the reader's
/// exchange, so the reader takes the middle only when it holds the newest publish, and the instances it gets are
/// ordered by publish time.</para>
/// <para>The writer must rewrite every field it publishes: <see cref="WriteTarget"/> holds what was published two
/// publishes earlier (or the factory's initial state).</para>
/// </remarks>
public sealed class SnapshotBuffer<T> where T : class
{
    private const int IndexMask = 3;
    private const int Fresh = 4;

    private readonly T[] m_slots;

    // Writer-owned index.
    private int m_back;

    // Shared: bits 0-1 the index, bit 2 Fresh. Changed only by Interlocked.Exchange; read early by Volatile.Read.
    private int m_middle;

    // Reader-owned index.
    private int m_front;

    /// <summary>
    /// Calls <paramref name="factory"/> exactly three times on the constructing thread.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="factory"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The factory returned null or the same instance twice.</exception>
    public SnapshotBuffer(Func<T> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var first = factory();
        var second = factory();
        var third = factory();

        if (first is null || second is null || third is null)
            throw new InvalidOperationException("The snapshot factory returned null.");
        if (ReferenceEquals(first, second) || ReferenceEquals(first, third) || ReferenceEquals(second, third))
            throw new InvalidOperationException("The snapshot factory returned the same instance twice.");

        m_slots = [first, second, third];
        m_middle = 0;
        m_back = 1;
        m_front = 2;
    }

    /// <summary>
    /// The writer thread: the instance to fill before <see cref="Publish"/>; the same instance until then. Its
    /// contents are stale.
    /// </summary>
    public T WriteTarget => m_slots[m_back];

    /// <summary>
    /// The writer thread. Makes <see cref="WriteTarget"/> the newest published instance and gives the writer another.
    /// Wait-free, allocates nothing.
    /// </summary>
    public void Publish()
    {
        // Full fence: every store into slots[m_back] is visible before the middle carries its index.
        m_back = Interlocked.Exchange(ref m_middle, m_back | Fresh) & IndexMask;
    }

    /// <summary>
    /// The reader thread. Returns the newest published instance (before any publish: the factory's third instance).
    /// The reader may read it until its next call and must never write it. Wait-free, allocates nothing.
    /// </summary>
    public T Acquire()
    {
        // A stale read of Fresh only delays the take to the next call; the exchange (a full fence) is what orders the
        // writer's stores before the reads of the instance it returns. It stores the front without Fresh.
        if ((Volatile.Read(ref m_middle) & Fresh) != 0)
            m_front = Interlocked.Exchange(ref m_middle, m_front) & IndexMask;

        return m_slots[m_front];
    }
}
