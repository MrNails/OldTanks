﻿using System.Runtime.CompilerServices;
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

    public static Vector3 ReplaceComponent(this Vector3 source, in Vector3 componentToReplace)
    {
        var local = source;
        
        if (componentToReplace.X != 0) local.X = componentToReplace.X;
        if (componentToReplace.Y != 0) local.Y = componentToReplace.Y;
        if (componentToReplace.Z != 0) local.Z = componentToReplace.Z;

        return local;
    }

    public static bool GreaterOrEqualThan(this Vector3 source, in Vector3 target)
    {
        return source.X >= target.X &&
               source.Y >= target.Y &&
               source.Z >= target.Z;
    }

    public static bool LowerOrEqualThan(this Vector3 source, in Vector3 target)
    {
        return source.X <= target.X &&
               source.Y <= target.Y &&
               source.Z <= target.Z;
    }
}