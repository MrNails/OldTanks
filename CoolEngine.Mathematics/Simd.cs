using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace CoolEngine.Mathematics;

/// <summary>
/// Moves the float vectors in and out of <see cref="Vector128{T}"/> with the portable APIs only. Unused lanes load as
/// zero; results read back only the vector's own lanes, so whatever an operation leaves in the unused lanes (0 / 0 is
/// NaN) never reaches the caller.
/// </summary>
internal static class Simd
{
    /// <summary>(x, y, 0, 0): the 8 bytes of the vector move as one 64-bit integer.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> Load(Vector2 value)
    {
        return Vector128.CreateScalar(Unsafe.BitCast<Vector2, ulong>(value)).AsSingle();
    }

    /// <summary>(x, y, z, 0).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> Load(Vector3 value)
    {
        return Vector128.Create(value.X, value.Y, value.Z, 0f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> Load(Vector4 value)
    {
        return Unsafe.BitCast<Vector4, Vector128<float>>(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ToVector2(Vector128<float> vector)
    {
        return Unsafe.BitCast<ulong, Vector2>(vector.AsUInt64().ToScalar());
    }

    /// <summary>The first 12 bytes of the register; the fourth lane is dropped.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToVector3(Vector128<float> vector)
    {
        return Unsafe.As<Vector128<float>, Vector3>(ref vector);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ToVector4(Vector128<float> vector)
    {
        return Unsafe.BitCast<Vector128<float>, Vector4>(vector);
    }

    /// <summary>IEEE negation: flips the sign bit of every lane, so +0 becomes -0 (a subtraction from zero would not).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> Negate(Vector128<float> vector)
    {
        return vector ^ Vector128.Create(float.NegativeZero);
    }
}
