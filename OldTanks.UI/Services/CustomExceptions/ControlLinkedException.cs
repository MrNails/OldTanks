namespace OldTanks.UI.Services.CustomExceptions;

public class ControlLinkedException : Exception
{
    public ControlLinkedException() { }

    public ControlLinkedException(string message) : base(message) { }

    public ControlLinkedException(string message, Exception inner) : base(message, inner) { }
}
