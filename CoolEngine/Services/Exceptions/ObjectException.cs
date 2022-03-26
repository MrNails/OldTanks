namespace CoolEngine.Services.Exceptions;

public class ObjectException : Exception
{
    public ObjectException(Type type, string message)
        : base(message)
    {
        ProblemType = type;
    }
    
    public ObjectException(Type type, string message, Exception inner)
        : base(message, inner)
    {
        ProblemType = type;
    }
    
    public Type ProblemType { get; }
}