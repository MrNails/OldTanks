using System.Diagnostics;
using CoolEngine.Core.Timing;

namespace CoolEngine.Core.Tests.TestSupport;

/// <summary>
/// A waiter the test drives: <see cref="WaitUntil"/> marks the loop as parked and blocks until the test calls
/// <see cref="Release"/> or someone calls <see cref="Wake"/> (a wake that comes while nobody waits is remembered once).
/// Time passes only through <see cref="Release"/>, so every park is a deterministic point.
/// </summary>
public sealed class ManualTickWaiter : ITickWaiter
{
    private static readonly TimeSpan s_hangDeadline = TimeSpan.FromSeconds(10);

    private readonly ManualTimestampSource m_source;
    private readonly object m_gate = new();

    // All guarded by m_gate.
    private bool m_parked;
    private bool m_releasePending;
    private bool m_wakePending;
    private int m_wakeCount;

    public ManualTickWaiter(ManualTimestampSource source)
    {
        m_source = source;
    }

    public int WakeCount
    {
        get
        {
            lock (m_gate)
                return m_wakeCount;
        }
    }

    public void WaitUntil(long timestamp)
    {
        lock (m_gate)
        {
            if (m_wakePending || m_releasePending)
            {
                m_wakePending = false;
                m_releasePending = false;
                return;
            }

            m_parked = true;
            Monitor.PulseAll(m_gate);

            while (!m_wakePending && !m_releasePending)
                Monitor.Wait(m_gate);

            m_wakePending = false;
            m_releasePending = false;
            m_parked = false;
        }
    }

    public void Wake()
    {
        lock (m_gate)
        {
            m_wakeCount++;
            m_wakePending = true;
            m_parked = false;
            Monitor.PulseAll(m_gate);
        }
    }

    /// <summary>
    /// Sets the source's time to <paramref name="now"/>, then lets the parked loop go on.
    /// </summary>
    public void Release(long now)
    {
        lock (m_gate)
        {
            m_source.Now = now;
            m_releasePending = true;
            m_parked = false;
            Monitor.PulseAll(m_gate);
        }
    }

    /// <summary>
    /// Blocks until the loop is parked in <see cref="WaitUntil"/>; fails the test after 10 s (a hang guard, not a
    /// timing assertion).
    /// </summary>
    public void WaitForParked()
    {
        WaitFor(() => m_parked, "The loop did not park");
    }

    /// <summary>
    /// Blocks until <see cref="WakeCount"/> reaches <paramref name="count"/>; fails the test after 10 s.
    /// </summary>
    public void WaitForWakeCount(int count)
    {
        WaitFor(() => m_wakeCount >= count, $"The waiter was not woken {count} time(s)");
    }

    public void Dispose()
    {
    }

    private void WaitFor(Func<bool> condition, string failure)
    {
        var stopwatch = Stopwatch.StartNew();
        lock (m_gate)
        {
            while (!condition())
            {
                var remaining = s_hangDeadline - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    Assert.Fail($"{failure} within {s_hangDeadline.TotalSeconds} s.");

                Monitor.Wait(m_gate, remaining);
            }
        }
    }
}
