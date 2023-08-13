using OpenTK.Mathematics;

namespace CoolEngine.PhysicEngine.Core;

public readonly struct Ray
{
    public Ray(float maxDepth, in Vector3 direction, in Vector3 startPoint)
    {
        MaxDepth = maxDepth;
        Direction = direction != Vector3.Zero ? direction.Normalized() : direction;
        StartPoint = startPoint;
        var res = Direction * Matrix3.Identity;
    }

    public float MaxDepth { get; }
    public Vector3 Direction { get; }
    public Vector3 StartPoint { get; }
}