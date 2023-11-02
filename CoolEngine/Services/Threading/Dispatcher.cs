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

    public T Invoke<T>(Func<T> callback)
    {
        T result = default!;
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