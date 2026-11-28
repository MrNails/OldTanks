namespace CoolEngine.Core.Timing;

/// <summary>
/// Monotonic integer timestamps; callable from any thread.
/// </summary>
public interface ITimestampSource
{
    /// <summary>
    /// Timestamp units per second.
    /// </summary>
    long Frequency { get; }

    long GetTimestamp();
}
