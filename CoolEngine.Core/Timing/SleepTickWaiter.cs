namespace CoolEngine.Core.Timing;

/// <summary>
/// The portable waiter: waits on an event in whole milliseconds until the time source reaches the timestamp or a
/// <see cref="Wake"/> sets the event. Its accuracy is the OS timer resolution (about 1 ms with a raised resolution,
/// up to about 16 ms on Windows without).
/// </summary>
public sealed class SleepTickWaiter : ITickWaiter
{
    private readonly ITimestampSource m_time;

    // Auto-reset: a Wake that comes while nobody waits stays set until the next wait consumes it.
    private readonly AutoResetEvent m_wake;

    /// <exception cref="ArgumentNullException"><paramref name="time"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="time"/>'s frequency is below 1.</exception>
    public SleepTickWaiter(ITimestampSource time)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentOutOfRangeException.ThrowIfLessThan(time.Frequency, 1L);

        m_time = time;

        // Created only after the checks, so a constructor that throws above leaves no event to dispose.
        m_wake = new AutoResetEvent(false);
    }

    /// <inheritdoc />
    public void WaitUntil(long timestamp)
    {
        while (true)
        {
            var now = m_time.GetTimestamp();
            if (now >= timestamp)
                return;

            if (m_wake.WaitOne(ToWholeMilliseconds((Int128)timestamp - now)))
                return;
        }
    }

    /// <inheritdoc />
    public void Wake()
    {
        m_wake.Set();
    }

    /// <summary>
    /// Closes the event.
    /// </summary>
    public void Dispose()
    {
        m_wake.Dispose();
    }

    // ceil(remaining / Frequency · 1000) ms, at most int.MaxValue (the loop waits again after that).
    private int ToWholeMilliseconds(Int128 remaining)
    {
        var frequency = (Int128)m_time.Frequency;
        var milliseconds = (remaining * 1000 + frequency - 1) / frequency;
        return milliseconds > int.MaxValue ? int.MaxValue : (int)milliseconds;
    }
}
