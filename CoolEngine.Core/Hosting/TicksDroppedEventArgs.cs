namespace CoolEngine.Core.Hosting;

/// <summary>
/// The backlog cap dropped ticks: simulation time has fallen behind wall time.
/// </summary>
public sealed class TicksDroppedEventArgs : EventArgs
{
    public TicksDroppedEventArgs(int droppedTicks, long totalDroppedTicks)
    {
        DroppedTicks = droppedTicks;
        TotalDroppedTicks = totalDroppedTicks;
    }

    /// <summary>
    /// Ticks dropped in this iteration.
    /// </summary>
    public int DroppedTicks { get; }

    /// <summary>
    /// Ticks dropped since the clock was created.
    /// </summary>
    public long TotalDroppedTicks { get; }
}
