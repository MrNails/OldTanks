using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;
using Serilog;

namespace CoolEngine.Services.Extensions;

public static class LoggerExtensions
{
    private delegate void LogLevelHandler(string messageTemplate, params object?[]? arumgents);

    private static readonly Dictionary<ILogger, DebugProc> s_debugProcs = new Dictionary<ILogger, DebugProc>();

    public static void AddGLMessageHandling(this ILogger logger)
    {
        DebugProc msgHandler = (source, type, id, severity, lenght, pMsg, pUserParam) => 
            OnDebugMessage(logger, source, type, id, severity, lenght, pMsg, pUserParam);
        
        s_debugProcs.Add(logger, msgHandler);
        
        GL.DebugMessageCallback(msgHandler, IntPtr.Zero);
        GL.Enable(EnableCap.DebugOutput);
        GL.Enable(EnableCap.DebugOutputSynchronous);
    }
    
    private static void OnDebugMessage(
        ILogger logger,
        DebugSource source,     // Source of the debugging message.
        DebugType type,         // Type of the debugging message.
        int id,                 // ID associated with the message.
        DebugSeverity severity, // Severity of the message.
        int length,             // Length of the string in pMessage.
        IntPtr pMessage,        // Pointer to message string.
        IntPtr pUserParam)      // The pointer you gave to OpenGL)
    {
        var message = Marshal.PtrToStringAnsi(pMessage, length);
        
        var strFormat = "[{0} source={1} type={2} id={3}] {4}";
        var logLevelHandler = (LogLevelHandler)logger.Information;
        
        switch (type)
        {
            case DebugType.DebugTypeError or 
                DebugType.DebugTypePerformance:
                logLevelHandler = logger.Error;
                break;
            case DebugType.DebugTypePortability or
                DebugType.DebugTypeDeprecatedBehavior or
                DebugType.DebugTypeUndefinedBehavior:
                logLevelHandler = logger.Warning;
                break;
        }

        logLevelHandler(strFormat, severity, source, type, id, message);
    }
}