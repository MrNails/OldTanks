using OpenTK.Mathematics;

namespace CoolEngine.Models;

public readonly struct Ray
{
    public Ray(Vector3 start, Vector3 end)
    {
        Start = start;
        End = end;
    }
    
    public Vector3 Start { get; }
    public Vector3 End { get; }

    public Vector3 RayDelta => Start - End;
}