using CoolEngine.Core.Hosting;

namespace CoolEngine.Core.Tests.TestSupport;

public enum TickCallKind
{
    Tick,
    PausedFrame
}

public readonly record struct TickCall(TickCallKind Kind, long Number, Thread Thread);

/// <summary>
/// Records each call (kind, number, thread); can throw on chosen ticks or in paused frames, call a hook inside a tick,
/// and block inside a chosen tick on a gate after signalling that it entered. Allocates nothing per call while the
/// record fits the initial capacity and no hook or fault is set. Plain properties: set them before a loop thread
/// starts, or between RunDue calls on the calling thread.
/// </summary>
public sealed class RecordingTickHandler : ITickHandler
{
    private static readonly TimeSpan s_hangDeadline = TimeSpan.FromSeconds(10);

    private readonly Lock m_lock = new();
    private readonly List<TickCall> m_calls;
    private readonly ManualResetEventSlim m_entered = new(false);
    private readonly ManualResetEventSlim m_gate = new(false);
    private Thread? m_firstThread;

    public RecordingTickHandler(int initialCapacity = 16)
    {
        m_calls = new List<TickCall>(initialCapacity);
    }

    /// <summary>
    /// Returns the exception to throw in the tick with the given number, or null.
    /// </summary>
    public Func<long, Exception?>? ThrowInTick { get; set; }

    /// <summary>
    /// Thrown in every paused frame when set.
    /// </summary>
    public Exception? ThrowInPausedFrame { get; set; }

    /// <summary>
    /// Called inside every tick, after it is recorded.
    /// </summary>
    public Action<long>? OnTick { get; set; }

    /// <summary>
    /// The tick that signals <see cref="WaitForEntered"/> and then blocks until <see cref="OpenGate"/>.
    /// </summary>
    public long? BlockInTick { get; set; }

    /// <summary>
    /// The thread of the first call.
    /// </summary>
    public Thread? FirstThread
    {
        get
        {
            lock (m_lock)
                return m_firstThread;
        }
    }

    public TickCall[] Calls
    {
        get
        {
            lock (m_lock)
                return m_calls.ToArray();
        }
    }

    public int TickCount
    {
        get
        {
            lock (m_lock)
                return m_calls.Count(c => c.Kind == TickCallKind.Tick);
        }
    }

    public int PausedFrameCount
    {
        get
        {
            lock (m_lock)
                return m_calls.Count(c => c.Kind == TickCallKind.PausedFrame);
        }
    }

    public void Tick(long tickNumber)
    {
        Record(TickCallKind.Tick, tickNumber);

        OnTick?.Invoke(tickNumber);

        if (BlockInTick == tickNumber)
        {
            m_entered.Set();
            m_gate.Wait();
        }

        if (ThrowInTick?.Invoke(tickNumber) is { } exception)
            throw exception;
    }

    public void PausedFrame(long lastTickNumber)
    {
        Record(TickCallKind.PausedFrame, lastTickNumber);

        if (ThrowInPausedFrame is { } exception)
            throw exception;
    }

    /// <summary>
    /// Blocks until the loop has entered <see cref="BlockInTick"/>; fails the test after 10 s.
    /// </summary>
    public void WaitForEntered()
    {
        if (!m_entered.Wait(s_hangDeadline))
            Assert.Fail($"The loop did not enter tick {BlockInTick} within {s_hangDeadline.TotalSeconds} s.");
    }

    public void OpenGate()
    {
        m_gate.Set();
    }

    private void Record(TickCallKind kind, long number)
    {
        var thread = Thread.CurrentThread;
        lock (m_lock)
        {
            m_firstThread ??= thread;
            m_calls.Add(new TickCall(kind, number, thread));
        }
    }
}
