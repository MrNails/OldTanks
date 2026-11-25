namespace CoolEngine.Services.Exceptions;

public class ShaderException : Exception
{
    public ShaderException(string message)
        : base(message)
    { }
    
    public ShaderException(string message, Exception inner)
        : base(message, inner)
    { }
}