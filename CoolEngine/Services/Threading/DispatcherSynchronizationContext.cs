namespace CoolEngine.Services.Threading;

public sealed class DispatcherSynchronizationContext : SynchronizationContext
{
    private readonly Dispatcher m_dispatcher;
    
    public DispatcherSynchronizationContext() : this(Application.Current.Dispatcher) { }
    
    public DispatcherSynchronizationContext(Dispatcher dispatcher)
    {
        m_dispatcher = dispatcher;
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        m_dispatcher.Invoke(d, state);
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        m_dispatcher.Post(d, state);
    }
}