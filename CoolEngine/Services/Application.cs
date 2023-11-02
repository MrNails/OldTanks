using CoolEngine.Services.Threading;
using Serilog;

namespace CoolEngine.Services;

public class Application
{
    private static Application? s_instance;

    public static Application Current => s_instance;
    
    public Application()
    {
        if (s_instance == null)
        {
            s_instance = this;
        }
        else
        {
            throw new InvalidOperationException("Cannot create new instance of Application when it exists.");
        }
        
        Log.Logger.Information("Application thread {ThreadId}", Thread.CurrentThread.ManagedThreadId);

        Dispatcher = new Dispatcher();
    }

    public Dispatcher Dispatcher { get; }
}