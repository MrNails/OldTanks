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

    public static Vector3 SystemToGLVector(in System.Numerics.Vector3 source) => new (source.X, source.Y, source.Z);
    public static  System.Numerics.Vector3  GLToSystemVector(in Vector3 source) => new (source.X, source.Y, source.Z);
}