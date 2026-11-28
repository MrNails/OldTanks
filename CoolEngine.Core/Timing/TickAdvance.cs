namespace CoolEngine.Core.Timing;

/// <summary>
/// The answer of one <see cref="FixedTickClock.Advance"/> call.
/// </summary>
/// <param name="Ticks">Advancing ticks to run now (steps included while paused).</param>
/// <param name="DroppedTicks">Ticks dropped by the backlog cap in this call; gone for good.</param>
/// <param name="PausedFrame">Paused, no tick to run, and at least one period has passed on the grid.</param>
public readonly record struct TickAdvance(int Ticks, int DroppedTicks, bool PausedFrame);
