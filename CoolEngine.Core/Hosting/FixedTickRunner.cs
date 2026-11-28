using CoolEngine.Core.Timing;

namespace CoolEngine.Core.Hosting;

/// <summary>
/// A <see cref="FixedTickClock"/>, an <see cref="ITickHandler"/> and the fault handling between them.
/// <see cref="RunDue"/> is one inline iteration, so a host without threads (a server loop, a browser) calls it
/// directly; <see cref="FixedTickThread"/> calls it on its own thread.
/// </summary>
/// <remarks>
/// Threads: <see cref="Start"/>, <see cref="RunDue"/>, <see cref="TickNumber"/> and <see cref="NextDueTimestamp"/>
/// on the loop thread only; <see cref="Pause"/>, <see cref="Resume"/>, <see cref="Step"/> and
/// <see cref="IsPaused"/> from any thread. Requests are stored in a volatile pause flag and an interlocked step
/// counter, and applied at the start of the next <see cref="RunDue"/>; the clock, the tick number and the fault state
/// belong to the loop thread.
/// </remarks>
public sealed class FixedTickRunner
{
    private readonly FixedTickClock m_clock;
    private readonly ITickHandler m_handler;

    // Requests from any thread, read by RunDue on the loop thread.
    private volatile bool m_pauseRequested;
    private int m_stepRequests;
    private volatile bool m_stopRequested;

    // Loop thread only.
    private long m_tickNumber;
    private long m_lastTimestamp;

    // Loop thread only: the latest handler call threw, and since then no handler call has succeeded, the loop has not
    // found the runner running again (a Resume stored after the fault's pause) and has applied no Step, so a further
    // fault is part of the same run of consecutive faults and is not raised again.
    private bool m_faultRunOpen;

    /// <summary>
    /// The runner owns <paramref name="clock"/> from here on: the caller must not touch it.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public FixedTickRunner(FixedTickClock clock, ITickHandler handler)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(handler);

        m_clock = clock;
        m_handler = handler;
    }

    /// <summary>
    /// Raised on the loop thread when a handler call throws, once per run of consecutive faults; a run ends when a
    /// handler call succeeds, when the loop finds the runner running again (a <see cref="Resume"/> stored after the
    /// fault's pause), or when it applies a <see cref="Step"/>. The runner has paused before it is raised.
    /// The fault's pause overrides a request that arrived while the faulting handler call ran: such a
    /// <see cref="Resume"/> is dropped, and so is such a <see cref="Step"/> when the runner was running; neither starts
    /// a new run of faults.
    /// </summary>
    public event EventHandler<FixedTickRunner, TickFaultEventArgs>? Faulted;

    /// <summary>
    /// Raised on the loop thread when the backlog cap drops ticks, before the iteration's ticks run.
    /// </summary>
    public event EventHandler<FixedTickRunner, TicksDroppedEventArgs>? TicksDropped;

    /// <summary>
    /// The loop thread: the number of the last started tick (0 before any).
    /// </summary>
    public long TickNumber => m_tickNumber;

    /// <summary>
    /// Any thread: the requested pause state.
    /// </summary>
    public bool IsPaused => m_pauseRequested;

    /// <summary>
    /// The loop thread: when <see cref="RunDue"/> next has work: the clock's next due timestamp, or the last
    /// timestamp given to <see cref="Start"/> or <see cref="RunDue"/> while a pause, resume or step request is
    /// pending.
    /// </summary>
    /// <exception cref="InvalidOperationException">Before <see cref="Start"/>.</exception>
    public long NextDueTimestamp => m_clock.IsStarted && HasPendingRequest ? m_lastTimestamp : m_clock.NextDueTimestamp;

    /// <summary>
    /// The clock's <see cref="FixedTickClock.TimestampFrequency"/>, for <see cref="FixedTickThread"/>'s check of its
    /// time source.
    /// </summary>
    internal long TimestampFrequency => m_clock.TimestampFrequency;

    private bool HasPendingRequest =>
        Volatile.Read(ref m_stepRequests) != 0 || m_pauseRequested != m_clock.IsPaused;

    /// <summary>
    /// The loop thread. Anchors the clock's grid at <paramref name="timestamp"/>.
    /// </summary>
    public void Start(long timestamp)
    {
        m_clock.Start(timestamp);
        m_lastTimestamp = timestamp;
    }

    /// <summary>
    /// The loop thread. Applies the pending requests, advances the clock to <paramref name="timestamp"/>, raises
    /// <see cref="TicksDropped"/> when the cap dropped ticks, then calls <see cref="ITickHandler.Tick"/> once per due
    /// tick or <see cref="ITickHandler.PausedFrame"/> once. When a handler call throws, the runner pauses, skips the
    /// rest of the batch and raises <see cref="Faulted"/> (once per run of consecutive faults). An exception thrown by
    /// a <see cref="Faulted"/> or <see cref="TicksDropped"/> subscriber leaves this method. Allocates nothing without a
    /// fault or a drop.
    /// </summary>
    /// <returns>The clock's answer for this iteration.</returns>
    /// <exception cref="InvalidOperationException">Before <see cref="Start"/>.</exception>
    public TickAdvance RunDue(long timestamp)
    {
        if (m_stopRequested)
            return default;

        ApplyRequests();

        m_lastTimestamp = timestamp;
        var advance = m_clock.Advance(timestamp);

        if (advance.DroppedTicks > 0)
            TicksDropped?.Invoke(this, new TicksDroppedEventArgs(advance.DroppedTicks, m_clock.TicksDropped));

        // The high-level caller of the handler, so the one place that catches: a fault pauses the simulation and is
        // reported; the handler's own per-tick code has no exception handling.
        Exception? fault = null;
        try
        {
            if (advance.PausedFrame)
            {
                if (!m_stopRequested)
                {
                    m_handler.PausedFrame(m_tickNumber);
                    m_faultRunOpen = false;
                }
            }
            else
            {
                for (var i = 0; i < advance.Ticks && !m_stopRequested; i++)
                {
                    m_handler.Tick(++m_tickNumber);
                    m_faultRunOpen = false;
                }
            }
        }
        catch (Exception exception)
        {
            fault = exception;
        }

        if (fault is not null)
            OnFault(fault, advance.PausedFrame);

        return advance;
    }

    /// <summary>
    /// Any thread. Takes effect at the next <see cref="RunDue"/>.
    /// </summary>
    public void Pause()
    {
        m_pauseRequested = true;
    }

    /// <summary>
    /// Any thread. Takes effect at the next <see cref="RunDue"/>; the first tick comes at the next grid point. One
    /// store; stored before a fault's pause it is overridden (the runner stays paused, no new <see cref="Faulted"/>);
    /// stored after, the runner resumes and its next fault raises <see cref="Faulted"/>.
    /// </summary>
    public void Resume()
    {
        m_pauseRequested = false;
    }

    /// <summary>
    /// Any thread. Counted: each call is one tick at the next <see cref="RunDue"/> while paused; discarded while
    /// running. A step that arrives while a running tick's handler call runs that then throws is discarded too (the
    /// fault's pause overrides it), so no tick runs after the fault; one that arrives while the runner was already
    /// paused is kept.
    /// </summary>
    public void Step()
    {
        Interlocked.Increment(ref m_stepRequests);
    }

    /// <summary>
    /// For <see cref="FixedTickThread"/>: the current <see cref="RunDue"/> returns after the handler call in progress;
    /// later calls run nothing.
    /// </summary>
    internal void RequestStop()
    {
        m_stopRequested = true;
    }

    // Loop thread.
    private void ApplyRequests()
    {
        var paused = m_pauseRequested;
        var steps = Interlocked.Exchange(ref m_stepRequests, 0);

        if (paused)
            m_clock.Pause();
        else
            m_clock.Resume();

        // While running, the clock ignores the steps (it returns false).
        for (var i = 0; i < steps; i++)
            m_clock.Step();

        // The flag reads false only after a Resume stored after the last fault's pause (the fault stores true), so
        // finding the runner running ends any run of faults, as an applied step does.
        if (!paused || steps > 0)
            m_faultRunOpen = false;
    }

    // Loop thread.
    private void OnFault(Exception fault, bool inPausedFrame)
    {
        // The clock's state is still the one ApplyRequests set before the handler calls.
        var wasRunning = !m_clock.IsPaused;

        // The fault's pause overrides the requests that arrived during the faulting call: a resume, and a step issued
        // while running (applied now it would run a tick after the fault). A step issued while paused stays. A Resume
        // stored before the flag store below is overridden by it; one stored after it is seen by the next
        // ApplyRequests, which ends the run of faults. A Step that lands between the clear and the flag store is kept
        // and runs one tick while paused.
        if (wasRunning)
            Interlocked.Exchange(ref m_stepRequests, 0);

        m_pauseRequested = true;
        m_clock.Pause();

        var raise = !m_faultRunOpen;
        m_faultRunOpen = true;

        if (raise)
            Faulted?.Invoke(this, new TickFaultEventArgs(fault, m_tickNumber, inPausedFrame));
    }
}
