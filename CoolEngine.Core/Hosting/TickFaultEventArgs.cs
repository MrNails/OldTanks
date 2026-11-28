namespace CoolEngine.Core.Hosting;

/// <summary>
/// A tick handler call threw; the runner has paused.
/// </summary>
public sealed class TickFaultEventArgs : EventArgs
{
    public TickFaultEventArgs(Exception exception, long tickNumber, bool inPausedFrame)
    {
        Exception = exception;
        TickNumber = tickNumber;
        InPausedFrame = inPausedFrame;
    }

    public Exception Exception { get; }

    /// <summary>
    /// The tick that threw, or the last started tick when <see cref="InPausedFrame"/>.
    /// </summary>
    public long TickNumber { get; }

    public bool InPausedFrame { get; }
}
