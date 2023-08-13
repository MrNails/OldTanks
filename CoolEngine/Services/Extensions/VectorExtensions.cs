using System.Runtime.CompilerServices;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Extensions;

public static class VectorExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 SystemToGLVector4(in System.Numerics.Vector4 source) => new (source.X, source.Y, source.Z, source.W);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.Numerics.Vector4 GLToSystemVector3(in Vector4 source) => new (source.X, source.Y, source.Z, source.W);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 SystemToGLVector3(in System.Numerics.Vector3 source) => new (source.X, source.Y, source.Z);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.Numerics.Vector3 GLToSystemVector3(in Vector3 source) => new (source.X, source.Y, source.Z);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 SystemToGLVector2(in System.Numerics.Vector2 source) => new (source.X, source.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.Numerics.Vector2 GLToSystemVector2(in Vector2 source) => new (source.X, source.Y);
}