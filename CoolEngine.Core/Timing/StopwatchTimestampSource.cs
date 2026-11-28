using System.Diagnostics;

namespace CoolEngine.Core.Timing;

/// <summary>
/// <see cref="Stopwatch"/>'s timestamps. No state; any thread.
/// </summary>
public sealed class StopwatchTimestampSource : ITimestampSource
{
    public long Frequency => Stopwatch.Frequency;

    public long GetTimestamp() => Stopwatch.GetTimestamp();
}
