namespace CoolEngine.Core.Timing;

/// <summary>
/// A pure fixed-tick state machine fed integer timestamps: it answers how many ticks are due now, and handles pause,
/// single step and the backlog cap. It has no thread, no sleep and no time source. Not thread-safe: one thread only.
/// </summary>
/// <remarks>
/// Exact integer arithmetic: tick <c>k</c> after <see cref="Start"/> at <c>t0</c> is due at exactly
/// <c>t0 + ceil(k · TimestampFrequency / TicksPerSecond)</c>. Ticks dropped by the cap and time spent paused keep the
/// sub-tick phase, so the grid never moves.
/// </remarks>
public sealed class FixedTickClock
{
    private long m_lastTimestamp;

    // The sub-tick phase in units of timestamp × ticks; always in [0, TimestampFrequency).
    private long m_accumulator;

    private bool m_started;
    private bool m_paused;
    private int m_pendingSteps;
    private long m_ticksIssued;
    private long m_ticksDropped;

    /// <summary>
    /// Creates a running (not paused), not started clock. Every value comes from the host.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A value below 1, or
    /// <paramref name="timestampFrequency"/> × <paramref name="ticksPerSecond"/> above <c>long.MaxValue / 2</c>.</exception>
    public FixedTickClock(int ticksPerSecond, long timestampFrequency, int maxBacklogTicks)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ticksPerSecond, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(timestampFrequency, 1L);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBacklogTicks, 1);

        // F·R > long.MaxValue / 2  ⇔  F > (long.MaxValue / 2) / R for positive integers; the bound keeps r·R and
        // m_accumulator + r·R below long.MaxValue in Advance.
        if (timestampFrequency > long.MaxValue / 2 / ticksPerSecond)
            throw new ArgumentOutOfRangeException(nameof(timestampFrequency), timestampFrequency,
                "timestampFrequency × ticksPerSecond must not exceed long.MaxValue / 2.");

        TicksPerSecond = ticksPerSecond;
        TimestampFrequency = timestampFrequency;
        MaxBacklogTicks = maxBacklogTicks;
    }

    public int TicksPerSecond { get; }

    public long TimestampFrequency { get; }

    public int MaxBacklogTicks { get; }

    public bool IsStarted => m_started;

    public bool IsPaused => m_paused;

    public int PendingSteps => m_pendingSteps;

    /// <summary>
    /// Cumulative advancing ticks returned by <see cref="Advance"/>, steps included.
    /// </summary>
    public long TicksIssued => m_ticksIssued;

    /// <summary>
    /// Cumulative ticks dropped by the backlog cap.
    /// </summary>
    public long TicksDropped => m_ticksDropped;

    /// <summary>
    /// The timestamp at which <see cref="Advance"/> will next return a tick or a paused frame: the last timestamp while
    /// a step is pending, otherwise the next grid point.
    /// </summary>
    /// <exception cref="InvalidOperationException">Before <see cref="Start"/>.</exception>
    public long NextDueTimestamp
    {
        get
        {
            ThrowIfNotStarted();

            if (m_pendingSteps > 0)
                return m_lastTimestamp;

            // The smallest e with m_accumulator + e·R ≥ F; F − m_accumulator is in (0, F], so nothing overflows.
            return m_lastTimestamp + (TimestampFrequency - m_accumulator + TicksPerSecond - 1) / TicksPerSecond;
        }
    }

    /// <summary>
    /// Anchors the grid at <paramref name="timestamp"/>, clears the sub-tick phase and the pending steps; keeps the
    /// pause state and the counters. Callable again to re-anchor, which discards any backlog.
    /// </summary>
    public void Start(long timestamp)
    {
        m_lastTimestamp = timestamp;
        m_accumulator = 0;
        m_pendingSteps = 0;
        m_started = true;
    }

    /// <summary>
    /// Consumes the time elapsed since the last call. Running: returns the due ticks, at most
    /// <see cref="MaxBacklogTicks"/>, the rest reported as dropped. Paused: discards the elapsed time and returns the
    /// pending steps, or one paused frame when none is pending and a period has passed. A timestamp that goes
    /// backwards is ignored. Allocates nothing.
    /// </summary>
    /// <exception cref="InvalidOperationException">Before <see cref="Start"/>.</exception>
    public TickAdvance Advance(long timestamp)
    {
        ThrowIfNotStarted();

        var due = ConsumeElapsed(timestamp);

        if (m_paused)
        {
            var steps = m_pendingSteps;
            m_pendingSteps = 0;
            m_ticksIssued += steps;
            return new TickAdvance(steps, 0, due > 0 && steps == 0);
        }

        if (due <= MaxBacklogTicks)
        {
            m_ticksIssued += due;
            return new TickAdvance((int)due, 0, false);
        }

        // At the cap: the dropped ticks are gone for good, the phase in m_accumulator is kept (the grid does not move).
        var dropped = due - MaxBacklogTicks;
        m_ticksIssued += MaxBacklogTicks;
        m_ticksDropped += dropped;
        return new TickAdvance(MaxBacklogTicks, dropped > int.MaxValue ? int.MaxValue : (int)dropped, false);
    }

    /// <summary>
    /// Idempotent.
    /// </summary>
    public void Pause()
    {
        m_paused = true;
    }

    /// <summary>
    /// Idempotent; clears the pending steps. The first tick after a resume comes at the next grid point.
    /// </summary>
    public void Resume()
    {
        m_paused = false;
        m_pendingSteps = 0;
    }

    /// <summary>
    /// While paused: adds one pending tick (uncapped: each call is an explicit request) and returns <c>true</c>.
    /// While running: changes nothing and returns <c>false</c>.
    /// </summary>
    public bool Step()
    {
        if (!m_paused)
            return false;

        m_pendingSteps++;
        return true;
    }

    // Returns the ticks due on the grid since the last timestamp (saturating at long.MaxValue) and moves the grid on.
    private long ConsumeElapsed(long timestamp)
    {
        if (timestamp <= m_lastTimestamp)
            return 0;

        // The difference of two longs fits an ulong exactly when timestamp > m_lastTimestamp.
        var elapsed = unchecked((ulong)(timestamp - m_lastTimestamp));
        m_lastTimestamp = timestamp;

        var frequency = (ulong)TimestampFrequency;
        var rate = (ulong)TicksPerSecond;
        var wholeSeconds = elapsed / frequency;
        var remainder = elapsed % frequency;

        // remainder·R < F·R ≤ long.MaxValue / 2 and m_accumulator < F ≤ long.MaxValue / 2 (constructor bound).
        var phase = (ulong)m_accumulator + remainder * rate;
        m_accumulator = (long)(phase % frequency);
        var fromPhase = phase / frequency;

        if (wholeSeconds > (ulong)long.MaxValue / rate)
            return long.MaxValue;

        var due = wholeSeconds * rate;
        return due > (ulong)long.MaxValue - fromPhase ? long.MaxValue : (long)(due + fromPhase);
    }

    private void ThrowIfNotStarted()
    {
        if (!m_started)
            throw new InvalidOperationException("The clock has not been started.");
    }
}
