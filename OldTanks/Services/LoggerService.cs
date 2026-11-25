using Serilog;

namespace OldTanks.Services;

public sealed class LoggerService
{
    public ILogger CreateLogger(string fileName = "log.txt")
    {
        return new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("log.txt")
            .CreateLogger();
    } 
}