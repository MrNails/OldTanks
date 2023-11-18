using System.Runtime.CompilerServices;
using OpenTK.Mathematics;

namespace CoolEngine.Services.Extensions;

public static class VectorExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ToGLVector4(this System.Numerics.Vector4 source) => new (source.X, source.Y, source.Z, source.W);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.Numerics.Vector4 ToSystemVector4(this Vector4 source) => new (source.X, source.Y, source.Z, source.W);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToGLVector3(this System.Numerics.Vector3 source) => new (source.X, source.Y, source.Z);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.Numerics.Vector3 ToSystemVector3(this Vector3 source) => new (source.X, source.Y, source.Z);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ToGLVector2(this System.Numerics.Vector2 source) => new (source.X, source.Y);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static System.Numerics.Vector2 ToSystemVector2(this Vector2 source) => new (source.X, source.Y);
}