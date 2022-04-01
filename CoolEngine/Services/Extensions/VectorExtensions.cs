using OpenTK.Mathematics;

namespace CoolEngine.Services.Extensions;

public static class VectorExtensions
{
    public static bool GreaterThan(in Vector3 left, in Vector3 right)
    {
        return left.X > right.X &&
               left.Y > right.Y &&
               left.Z > right.Z;
    }
    
    public static bool LoverThan(in Vector3 left, in Vector3 right)
    {
        return left.X < right.X &&
               left.Y < right.Y &&
               left.Z < right.Z;
    }
}