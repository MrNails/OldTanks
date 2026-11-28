using CoolEngine.Core.Timing;

namespace CoolEngine.Core.Hosting;

/// <summary>
/// Runs a <see cref="FixedTickRunner"/> on its own background thread, paced by an <see cref="ITickWaiter"/>. Single
/// use. On single-threaded WebAssembly <see cref="Start"/> throws <see cref="PlatformNotSupportedException"/>: such a
/// host calls the runner inline.
/// </summary>
/// <remarks>
/// Every member may be called from any thread; <see cref="Stop"/> and <see cref="Dispose"/> not from the loop thread
/// (a tick, a paused frame or a <see cref="FixedTickRunner.Faulted"/> handler). Stopping is a volatile flag, the
/// runner's stop request and a wake, then <see cref="Thread.Join()"/>, which publishes everything the loop thread
/// wrote to the stopping thread.
/// </remarks>
public sealed class FixedTickThread : IDisposable
{
    private readonly FixedTickRunner m_runner;
    private readonly ITimestampSource m_time;
    private readonly ITickWaiter m_waiter;
    private readonly string m_threadName;

    // Guards m_thread's assignment, m_stopRequested's first write and m_disposed against concurrent Start, Stop and
    // Dispose. Never held across a call into user code or a Join.
    private readonly Lock m_stateLock = new();

    // Written under m_stateLock once the thread has started; read by IsRunning from any thread.
    private Thread? m_thread;
    private bool m_disposed;

    // Read by the loop thread on every iteration.
    private volatile bool m_stopRequested;

    // Written by the loop thread as its last action.
    private volatile bool m_loopEnded;

    /// <summary>
    /// Does not own <paramref name="waiter"/>: the host disposes it after <see cref="Stop"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="time"/>'s frequency differs from the runner's clock's
    /// timestamp frequency (the loop would run at the wrong rate).</exception>
    public FixedTickThread(FixedTickRunner runner, ITimestampSource time, ITickWaiter waiter, string threadName)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(waiter);
        ArgumentNullException.ThrowIfNull(threadName);

        if (time.Frequency != runner.TimestampFrequency)
            throw new ArgumentException(
                $"The time source's frequency ({time.Frequency}) differs from the clock's timestamp frequency ({runner.TimestampFrequency}).",
                nameof(time));

        m_runner = runner;
        m_time = time;
        m_waiter = waiter;
        m_threadName = threadName;
    }

    /// <summary>
    /// Any thread: started, and the loop thread has not ended.
    /// </summary>
    public bool IsRunning => Volatile.Read(ref m_thread) is not null && !m_loopEnded;

    /// <summary>
    /// Any thread: the runner's requested pause state.
    /// </summary>
    public bool IsPaused => m_runner.IsPaused;

    /// <summary>
    /// Any thread. Starts the background loop thread: anchor the runner at the current timestamp, then run what is
    /// due and wait for the next due timestamp until stopped.
    /// </summary>
    /// <exception cref="InvalidOperationException">Started before.</exception>
    /// <exception cref="ObjectDisposedException">After <see cref="Dispose"/>.</exception>
    public void Start()
    {
        lock (m_stateLock)
        {
            ObjectDisposedException.ThrowIf(m_disposed, this);
            if (m_thread is not null)
                throw new InvalidOperationException("The tick thread has been started before.");

            var thread = new Thread(Loop) { IsBackground = true, Name = m_threadName };
            thread.Start();

            // Assigned only once started: a Start that throws leaves the host not started.
            Volatile.Write(ref m_thread, thread);
        }
    }

    /// <summary>
    /// Any thread. Forwards to the runner, then wakes the loop.
    /// </summary>
    public void Pause()
    {
        m_runner.Pause();
        m_waiter.Wake();
    }

    /// <summary>
    /// Any thread. Forwards to the runner, then wakes the loop.
    /// </summary>
    public void Resume()
    {
        m_runner.Resume();
        m_waiter.Wake();
    }

    /// <summary>
    /// Any thread. Forwards to the runner, then wakes the loop.
    /// </summary>
    public void Step()
    {
        m_runner.Step();
        m_waiter.Wake();
    }

    /// <summary>
    /// Any thread except the loop thread. Returns after the handler call in progress has returned; no further tick or
    /// paused frame starts. A call that never returns blocks it. Before <see cref="Start"/> and a second time: no
    /// effect (the second call only joins).
    /// </summary>
    /// <exception cref="InvalidOperationException">Called on the loop thread.</exception>
    public void Stop()
    {
        Thread? thread;
        bool first;
        lock (m_stateLock)
        {
            thread = m_thread;
            if (thread is null)
                return;
            if (thread.ManagedThreadId == Environment.CurrentManagedThreadId)
                throw new InvalidOperationException("The tick thread cannot be stopped from its own loop thread.");

            first = !m_stopRequested;
            m_stopRequested = true;
        }

        if (first)
        {
            // The flags are set before the wake, so the loop sees them when it wakes.
            m_runner.RequestStop();
            m_waiter.Wake();
        }

        thread.Join();
    }

    /// <summary>
    /// Marks the host disposed, then <see cref="Stop"/>. A <see cref="Start"/> racing it either ran first and its loop
    /// is stopped here, or throws <see cref="ObjectDisposedException"/>: the loop never outlives this call.
    /// </summary>
    public void Dispose()
    {
        // Disposed first, under the lock that Start holds while it checks m_disposed and assigns m_thread: a Start that
        // takes the lock after this throws, and one that took it before has assigned m_thread, which the Stop below
        // reads under the same lock and joins.
        lock (m_stateLock)
            m_disposed = true;

        Stop();
    }

    // The loop thread.
    private void Loop()
    {
        m_runner.Start(m_time.GetTimestamp());

        while (!m_stopRequested)
        {
            m_runner.RunDue(m_time.GetTimestamp());

            if (m_stopRequested)
                break;

            m_waiter.WaitUntil(m_runner.NextDueTimestamp);
        }

        m_loopEnded = true;
    }
}
