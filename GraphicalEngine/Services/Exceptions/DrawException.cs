namespace GraphicalEngine.Services.Exceptions;

public class DrawException : Exception
{
    public DrawException(string message)
        : base(message)
    { }
    
    public DrawException(string message, Exception inner) 
        : base(message, inner)
    { }
}