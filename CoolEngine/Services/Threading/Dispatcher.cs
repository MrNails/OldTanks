using System.Collections.Concurrent;
using Serilog;

namespace CoolEngine.Services.Threading;

public sealed class Dispatcher
{
    private readonly Thread m_ownerThread;

    private readonly ConcurrentQueue<IDispatcherOperation> m_operations;
    
    public Dispatcher()
    {
        m_ownerThread = Thread.CurrentThread;
        m_operations = new ConcurrentQueue<IDispatcherOperation>();
    }
    
    private bool IsCurrentThread => m_ownerThread.ManagedThreadId == Environment.CurrentManagedThreadId;
    
    /// <summary>
    /// Execute callback synchronously on thread where invokes HandleQueue
    /// </summary>
    /// <param name="callback">Callback that will be invoked</param>
    public void Invoke(Action callback)
    {
        if (IsCurrentThread)
        {
            callback();
        }
        else
        {
            var op = new DispatcherOperation(callback);
            
            m_operations.Enqueue(op);
            
            op.Wait();
        }
    }
    
    /// <summary>
    /// Execute callback synchronously on thread where invokes HandleQueue
    /// </summary>
    /// <param name="callback">Callback that will be invoked</param>
    /// <param name="state">State for callback</param>
    public void Invoke(Action<object?> callback, object? state)
    {
        if (IsCurrentThread)
        {
            callback(state);
        }
        else
        {
            var op = new DispatcherOperation(callback, state);
            
            m_operations.Enqueue(op);
            
            op.Wait();
        }
    }
    
    /// <summary>
    /// Execute callback synchronously on thread where invokes HandleQueue
    /// </summary>
    /// <param name="callback">Callback that will be invoked</param>
    /// <param name="state">State for callback</param>
    public void Invoke(Delegate callback, object? state)
    {
        if (IsCurrentThread)
        {
            callback.DynamicInvoke(state as object?[] ?? state);
        }
        else
        {
            var op = new DispatcherOperation(callback, state);
            
            m_operations.Enqueue(op);
            
            op.Wait();
        }
    }

    /// <summary>
    /// Execute callback with result synchronously on thread where invokes HandleQueue
    /// </summary>
    /// <param name="callback">Callback that will be invoked</param>
    public T? Invoke<T>(Func<T?> callback)
    {
        T? result = default!;
        if (IsCurrentThread)
        {
            result = callback();
        }
        else
        {
            void OpExecutedHandler(object? s) => result = (T)s!;

            var op = new DispatcherOperation<T>(callback);
            op.Executed += OpExecutedHandler;
            
            m_operations.Enqueue(op);
            
            op.Wait();
            op.Executed -= OpExecutedHandler;
        }

        return result;
    }
    
    /// <summary>
    /// Execute callback with result synchronously on thread where invokes HandleQueue
    /// </summary>
    /// <param name="callback">Callback that will be invoked</param>
    /// <param name="state">State for callback</param>
    public T? Invoke<T>(Func<object?, T?> callback, object? state)
    {
        T? result = default!;
        if (IsCurrentThread)
        {
            result = callback(state);
        }
        else
        {
            void OpExecutedHandler(object? s) => result = (T)s!;

            var op = new DispatcherOperation<T>(callback, state);
            op.Executed += OpExecutedHandler;
            
            m_operations.Enqueue(op);
            
            op.Wait();
            op.Executed -= OpExecutedHandler;
        }

        return result;
    }
    
    /// <summary>
    /// Execute callback with result synchronously on thread where invokes HandleQueue
    /// </summary>
    /// <param name="callback">Callback that will be invoked</param>
    /// <param name="state">State for callback</param>
    public T? Invoke<T>(Delegate callback, object? state)
    {
        T? result = default!;
        if (IsCurrentThread)
        {
            result = callback.DynamicInvoke(state as object?[] ?? state) is T tmpResult ? tmpResult : default;
        }
        else
        {
            void OpExecutedHandler(object? s) => result = (T)s!;

            var op = new DispatcherOperation<T>(callback, state);
            op.Executed += OpExecutedHandler;
            
            m_operations.Enqueue(op);
            
            op.Wait();
            op.Executed -= OpExecutedHandler;
        }

        return result;
    }

    /// <summary>
    /// Execute callback asynchronously on thread where invokes HandleQueue
    /// </summary>
    /// <param name="callback">Callback that will be invoked</param>
    public void Post(Action callback)
    {
        if (IsCurrentThread)
        {
            callback();
        }
        else
        {
            var op = new DispatcherOperation(callback);
            
            m_operations.Enqueue(op);
        }
    }
    
    /// <summary>
    /// Execute callback asynchronously on thread where invokes HandleQueue
    /// </summary>
    /// <param name="callback">Callback that will be invoked</param>
    /// <param name="state">State for callback</param>
    public void Post(Action<object?> callback, object? state)
    {
        if (IsCurrentThread)
        {
            callback(state);
        }
        else
        {
            var op = new DispatcherOperation(callback, state);
            
            m_operations.Enqueue(op);
        }
    }
    
    /// <summary>
    /// Execute callback asynchronously on thread where invokes HandleQueue
    /// </summary>
    /// <param name="callback">Callback that will be invoked</param>
    /// <param name="state">State for callback</param>
    public void Post(Delegate callback, object? state)
    {
        if (IsCurrentThread)
        {
            callback.DynamicInvoke(state as object?[] ?? state);
        }
        else
        {
            var op = new DispatcherOperation(callback, state);
            
            m_operations.Enqueue(op);
        }
    }
    
    /// <summary>
    /// Handle operations queue if it have elements
    /// </summary>
    public void HandleQueue()
    {
        while (!m_operations.IsEmpty)
        {
            if (m_operations.TryDequeue(out var op))
            {
                op.Execute();
            }
        }
    }
}