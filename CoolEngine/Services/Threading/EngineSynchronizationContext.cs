namespace CoolEngine.Services.Threading;

public sealed class EngineSynchronizationContext : SynchronizationContext
{
    
    
    public override void Post(SendOrPostCallback d, object? state)
    {
        base.Post(d, state);
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        // Thread.CurrentThread.ExecutionContext.
    }
}