using CoolEngine.Core.Timing;

namespace CoolEngine.Core.Tests.TestSupport;

/// <summary>
/// A time source the test moves by hand; any thread may read it.
/// </summary>
public sealed class ManualTimestampSource : ITimestampSource
{
    private long m_now;

    public ManualTimestampSource(long frequency, long now = 0)
    {
        Frequency = frequency;
        m_now = now;
    }

    public long Frequency { get; }

    public long Now
    {
        get => Volatile.Read(ref m_now);
        set => Volatile.Write(ref m_now, value);
    }

    public long GetTimestamp() => Now;
}
