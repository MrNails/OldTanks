using OldTanks.UI.Services;

namespace OldTanks.UI.Tests;

// The handler is one per process and cannot be reset: it is created once here, and every test uses unique control names.
[SetUpFixture]
public sealed class ControlHandlerSetUp
{
    [OneTimeSetUp]
    public void CreateControlHandler()
    {
        if (ControlHandler.Current == null)
            _ = new ControlHandler();
    }
}
