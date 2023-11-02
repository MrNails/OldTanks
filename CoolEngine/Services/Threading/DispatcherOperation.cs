using System.Runtime.CompilerServices;
using Serilog;

namespace CoolEngine.Services.Threading;

public interface IDispatcherOperation
{
    event Action<object?>? Executed;

    void Execute();
    void Wait();
}

public sealed class DispatcherOperation : IDispatcherOperation
{
    private readonly Action m_callback;
    private readonly TaskCompletionSource m_completionSource;

    public event Action<object?>? Executed;

    public DispatcherOperation(Action callback)
    {
        m_callback = callback;
        m_completionSource = new TaskCompletionSource();
    }
    
    public void Execute()
    {
        try
        {
            m_callback();
            m_completionSource.SetResult();
        }
        catch (Exception e)
        { 
            m_completionSource.SetException(e);
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
    private readonly Func<T> m_callback;
    private readonly TaskCompletionSource<T> m_completionSource;

    public event Action<object?>? Executed;

    public DispatcherOperation(Func<T> callback)
    {
        m_callback = callback;
        m_completionSource = new TaskCompletionSource<T>();
    }
    
    public void Execute()
    {
        T result = default!;
        try
        {
            result = m_callback();
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