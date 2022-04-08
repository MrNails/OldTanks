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

    public static Vector4 SystemToGLVector4(in System.Numerics.Vector4 source) => new (source.X, source.Y, source.Z, source.W);
    public static  System.Numerics.Vector4  GLToSystemVector3(in Vector4 source) => new (source.X, source.Y, source.Z, source.W);
    
    public static Vector3 SystemToGLVector3(in System.Numerics.Vector3 source) => new (source.X, source.Y, source.Z);
    public static  System.Numerics.Vector3  GLToSystemVector3(in Vector3 source) => new (source.X, source.Y, source.Z);
    
    public static Vector2 SystemToGLVector2(in System.Numerics.Vector2 source) => new (source.X, source.Y);
    public static  System.Numerics.Vector2  GLToSystemVector2(in Vector2 source) => new (source.X, source.Y);
}