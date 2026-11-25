using Serilog;
using Serilog.Core;

namespace OldTanks.Services;

/// <summary>
/// Owns the one root logger of the process: console and <c>log.txt</c> in the working directory, level Debug.
/// Every logger handed out by <see cref="CreateLogger"/> writes through it.
/// Create one instance per process, because a second one opens a second file sink on <c>log.txt</c>, which fails (KI-004).
/// </summary>
public sealed class LoggerService
{
    private const string LogFileName = "log.txt";
    private const string RootSource = "OldTanks";
    private const string ConsoleTemplate = "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";
    private const string FileTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}";

    private readonly Logger m_rootLogger;

    public LoggerService()
    {
        m_rootLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty(Serilog.Core.Constants.SourceContextPropertyName, RootSource)
            .WriteTo.Console(outputTemplate: ConsoleTemplate)
            .WriteTo.File(LogFileName, outputTemplate: FileTemplate)
            .CreateLogger();
    }

    /// <summary>
    /// Returns the root logger, tagged with <paramref name="source"/> when one is given. Never creates a sink:
    /// every logger writes to the same console and <c>log.txt</c>. The source appears in brackets on every line.
    /// </summary>
    /// <param name="source">The name shown on every line of this logger; the root source <c>OldTanks</c> when null or blank</param>
    public ILogger CreateLogger(string? source = null)
    {
        return string.IsNullOrWhiteSpace(source)
            ? m_rootLogger
            : m_rootLogger.ForContext(Serilog.Core.Constants.SourceContextPropertyName, source);
    }
}
