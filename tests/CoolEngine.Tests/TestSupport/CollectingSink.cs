using System.Collections.Concurrent;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CoolEngine.Tests.TestSupport;

/// <summary>
/// A Serilog sink that keeps every event it receives, for assertions.
/// </summary>
public sealed class CollectingSink : ILogEventSink
{
    public ConcurrentQueue<LogEvent> Events { get; } = new();

    public void Emit(LogEvent logEvent)
    {
        Events.Enqueue(logEvent);
    }
}

/// <summary>
/// Points the static <see cref="Log.Logger"/> at a <see cref="CollectingSink"/> for one test and restores it afterwards.
/// Call <see cref="Start"/> in [SetUp] and <see cref="Stop"/> in [TearDown].
/// </summary>
internal sealed class LogCapture
{
    private ILogger? m_savedLogger;
    private Logger? m_logger;

    public CollectingSink Sink { get; private set; } = new();

    public void Start()
    {
        m_savedLogger = Log.Logger;
        Sink = new CollectingSink();
        m_logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(Sink).CreateLogger();
        Log.Logger = m_logger;
    }

    public void Stop()
    {
        m_logger?.Dispose();
        m_logger = null;

        if (m_savedLogger is not null)
            Log.Logger = m_savedLogger;
        m_savedLogger = null;
    }
}
