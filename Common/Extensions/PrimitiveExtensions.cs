using System.Runtime.CompilerServices;

namespace Common.Extensions;

public static class PrimitiveExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ApproximateEqualOrGreater(this float left, float right, float epsilon = float.Epsilon)
    {
        return left.ApproximateEqual(right, epsilon) || left > right;
    } 
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ApproximateEqualOrLower(this float left, float right, float epsilon = float.Epsilon)
    {
        return left.ApproximateEqual(right, epsilon) || left < right;
    } 
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ApproximateEqual(this float left, float right, float epsilon = float.Epsilon)
    {
        return Math.Abs(left - right) < epsilon;
    } 
}