using System.Runtime.CompilerServices;

namespace CoolEngine.Mathematics;

/// <summary>
/// Float constants of the π family and the conversions between degrees and radians. The whole library takes angles in
/// radians; these conversions are the only place degrees appear. Scalar clamp and lerp come from the BCL
/// (<see cref="Math.Clamp(float, float, float)"/>, <see cref="float.Lerp(float, float, float)"/>).
/// </summary>
public static class MathHelper
{
    /// <summary>π as a float, equal to <see cref="MathF.PI"/>.</summary>
    public const float Pi = MathF.PI;

    /// <summary>2π as a float.</summary>
    public const float TwoPi = 2f * MathF.PI;

    /// <summary>π / 2 as a float.</summary>
    public const float PiOver2 = MathF.PI / 2f;

    /// <summary>π / 4 as a float.</summary>
    public const float PiOver4 = MathF.PI / 4f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DegreesToRadians(float degrees)
    {
        return degrees * (Pi / 180f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DegreesToRadians(double degrees)
    {
        return degrees * (Math.PI / 180.0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float RadiansToDegrees(float radians)
    {
        return radians * (180f / Pi);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double RadiansToDegrees(double radians)
    {
        return radians * (180.0 / Math.PI);
    }
}
