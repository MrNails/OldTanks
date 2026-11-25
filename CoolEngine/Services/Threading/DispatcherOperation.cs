namespace CoolEngine.Services.Threading;

public interface IDispatcherOperation
{
    event Action<object?>? Executed;

    void Execute();
    void Wait();
}

public sealed class DispatcherOperation : IDispatcherOperation
{
    private readonly Delegate m_callback;
    private readonly TaskCompletionSource m_completionSource;
    private readonly object? m_state;
    private readonly bool m_isPosted;

    public event Action<object?>? Executed;

    public DispatcherOperation(Action callback) : this(callback, null) { }
    public DispatcherOperation(Action<object?> callback, object? state) : this((Delegate)callback, state) { }
    public DispatcherOperation(Delegate callback, object? state) : this(callback, state, isPosted: false) { }

    /// <summary>
    /// Creates an operation for the dispatcher's queue.
    /// </summary>
    /// <param name="callback">Callback that will be invoked</param>
    /// <param name="state">State for callback</param>
    /// <param name="isPosted">True for an operation queued by <c>Dispatcher.Post</c>, which nobody waits on:
    /// an exception from its callback is logged at Error in <see cref="Execute"/></param>
    internal DispatcherOperation(Delegate callback, object? state, bool isPosted)
    {
        m_callback = callback;
        m_state = state;
        m_isPosted = isPosted;
        m_completionSource = new TaskCompletionSource();
    }
    
    public void Execute()
    {
        var isDynamicInvoke = false;
        try
        {
            switch (m_callback)
            {
                case Action action:
                    action();
                    break;
                case Action<object?> actionState:
                    actionState(m_state);
                    break;
                default:
                    isDynamicInvoke = true;
                    m_callback.DynamicInvoke(m_state as object?[] ?? m_state);
                    break;
            }

            m_completionSource.SetResult();
        }
        catch (Exception e)
        { 
            m_completionSource.SetException(e);

            if (m_isPosted)
            {
                // Nobody waits on a posted operation: mark its exception observed and log it instead
                _ = m_completionSource.Task.Exception;

                var error = isDynamicInvoke && e is System.Reflection.TargetInvocationException { InnerException: { } inner }
                    ? inner
                    : e;
                var name = $"{m_callback.Method.DeclaringType?.FullName}.{m_callback.Method.Name}";

                Serilog.Log.Logger.Error(error, "A callback posted to the dispatcher threw an exception: {Callback}", name);
            }
        }
        finally
        {
            Executed?.Invoke(null);
        }
    }

    public void Wait()
    {
        m_completionSource.Task.Wait();
    }
}

public sealed class DispatcherOperation<T> : IDispatcherOperation
{
    private readonly Delegate m_callback;
    private readonly TaskCompletionSource<T?> m_completionSource;
    private readonly object? m_state;

    public event Action<object?>? Executed;

    public DispatcherOperation(Func<T?> callback) : this(callback, null) { }
    public DispatcherOperation(Func<object?, T?> callback, object? state)  : this((Delegate)callback, state) { }
    public DispatcherOperation(Delegate callback, object? state)
    {
        m_callback = callback;
        m_completionSource = new TaskCompletionSource<T?>();
        m_state = state;
    }
    
    public void Execute()
    {
        T? result = default;
        try
        {
            result = m_callback switch
            {
                Func<T?> func => func(),
                Func<object?, T?> funcState => funcState(m_state),
                _ => m_callback.DynamicInvoke(m_state as object?[] ?? m_state) is T tmpResult ? tmpResult : default
            };
            
            m_completionSource.SetResult(result);
        }
        catch (Exception e)
        { 
            m_completionSource.SetException(e);
        }
        finally
        {
            Executed?.Invoke(result);
        }
    }

    public void Wait()
    {
        m_completionSource.Task.Wait();
    }
}