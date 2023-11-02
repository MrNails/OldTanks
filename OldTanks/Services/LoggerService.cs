using Serilog;
using Serilog.Sinks.FastConsole;

namespace OldTanks.Services;

public sealed class LoggerService
{
    public ILogger CreateLogger(string fileName = "log.txt")
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            // .WriteTo.FastConsole(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console()
            .WriteTo.File("log.txt")
            .CreateLogger();
    } 
}