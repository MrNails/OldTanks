namespace CoolEngine.Core.Hosting;

/// <summary>
/// The simulation side of a fixed-tick loop. Called on the loop thread (or the inline host's thread) only.
/// </summary>
public interface ITickHandler
{
    /// <summary>
    /// One fixed step. <paramref name="tickNumber"/> starts at 1 and counts every started tick.
    /// </summary>
    void Tick(long tickNumber);

    /// <summary>
    /// Paused, and a period has passed: apply commands and publish without stepping.
    /// <paramref name="lastTickNumber"/> is the number of the last started tick (0 before any).
    /// </summary>
    void PausedFrame(long lastTickNumber);
}
