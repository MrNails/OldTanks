using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CoolEngine.Mathematics;

/// <summary>
/// A double 3D vector: X, Y, Z in 24 bytes, with the API shape of <see cref="System.Numerics.Vector3"/> (its float twin)
/// and the same semantics: <see cref="Min"/> and <see cref="Max"/> propagate NaN and order -0 below +0, unary minus flips
/// the sign bit, <see cref="Normalize"/> of zero gives NaN. Own code, scalar on the fields (review A: no SIMD round trip
/// for 24 bytes), with no fused multiply-add and every sum left to right. Bit stability: every member (they use only
/// + − × ÷, sqrt and exact operations: negation, Abs, Min, Max, conversions) gives the same bits on every x64 CPU and
/// instruction set for finite inputs (a NaN's sign and payload are not covered).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Vector3d : IEquatable<Vector3d>
{
    public double X;
    public double Y;
    public double Z;

    public const int SizeInBytes = 24;

    /// <summary>Every component set to <paramref name="value"/>.</summary>
    public Vector3d(double value)
    {
        X = value;
        Y = value;
        Z = value;
    }

    public Vector3d(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3d(Vector2d xy, double z)
    {
        X = xy.X;
        Y = xy.Y;
        Z = z;
    }

    public static Vector3d Zero => default;

    public static Vector3d One => new(1.0);

    public static Vector3d UnitX => new(1.0, 0.0, 0.0);

    public static Vector3d UnitY => new(0.0, 1.0, 0.0);

    public static Vector3d UnitZ => new(0.0, 0.0, 1.0);

    /// <summary>0-based: X, Y, Z. Throws <see cref="ArgumentOutOfRangeException"/> outside 0 … 2.</summary>
    public double this[int index]
    {
        readonly get => index switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "A Vector3d index is 0, 1 or 2."),
        };
        set
        {
            switch (index)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                case 2: Z = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), index, "A Vector3d index is 0, 1 or 2.");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double Length()
    {
        return Math.Sqrt(LengthSquared());
    }

    /// <summary><c>X·X + Y·Y + Z·Z</c>, left to right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double LengthSquared()
    {
        return X * X + Y * Y + Z * Z;
    }

    /// <summary>Component-wise <see cref="Math.Abs(double)"/> (clears the sign bit).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d Abs(Vector3d value)
    {
        return new Vector3d(Math.Abs(value.X), Math.Abs(value.Y), Math.Abs(value.Z));
    }

    /// <summary><c>Min(Max(value, min), max)</c>, as System.Numerics; no check that <c>min &lt;= max</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d Clamp(Vector3d value, Vector3d min, Vector3d max)
    {
        return Min(Max(value, min), max);
    }

    /// <summary>The right-handed cross product: <c>Cross(UnitX, UnitY) == UnitZ</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d Cross(Vector3d left, Vector3d right)
    {
        return new Vector3d(
            left.Y * right.Z - left.Z * right.Y,
            left.Z * right.X - left.X * right.Z,
            left.X * right.Y - left.Y * right.X);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Distance(Vector3d value1, Vector3d value2)
    {
        return (value1 - value2).Length();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DistanceSquared(Vector3d value1, Vector3d value2)
    {
        return (value1 - value2).LengthSquared();
    }

    /// <summary><c>l.X·r.X + l.Y·r.Y + l.Z·r.Z</c>, left to right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Dot(Vector3d left, Vector3d right)
    {
        return left.X * right.X + left.Y * right.Y + left.Z * right.Z;
    }

    /// <summary><c>value1 + (value2 - value1) * amount</c>; <paramref name="amount"/> is not clamped, so values outside [0, 1] extrapolate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d Lerp(Vector3d value1, Vector3d value2, double amount)
    {
        return value1 + (value2 - value1) * amount;
    }

    /// <summary>Component-wise <see cref="Math.Max(double, double)"/>: NaN propagates, +0 is above -0 (System.Numerics' semantics).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d Max(Vector3d value1, Vector3d value2)
    {
        return new Vector3d(Math.Max(value1.X, value2.X), Math.Max(value1.Y, value2.Y), Math.Max(value1.Z, value2.Z));
    }

    /// <summary>Component-wise <see cref="Math.Min(double, double)"/>: NaN propagates, -0 is below +0 (System.Numerics' semantics).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d Min(Vector3d value1, Vector3d value2)
    {
        return new Vector3d(Math.Min(value1.X, value2.X), Math.Min(value1.Y, value2.Y), Math.Min(value1.Z, value2.Z));
    }

    /// <summary>
    /// The vector times the reciprocal of its length (<c>1 / Length()</c> once, then a multiply per component): at most
    /// 1 ulp from a true division per component. The zero vector gives NaN in every component (1 / 0 = +∞, 0 · ∞ = NaN; no check).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d Normalize(Vector3d value)
    {
        return value * (1.0 / value.Length());
    }

    /// <summary>
    /// <paramref name="value"/> rotated by the unit quaternion <paramref name="rotation"/>:
    /// <c>v + 2w(u × v) + 2u × (u × v)</c> with <c>u</c> the vector part, computed as <c>t = 2(u × v)</c>,
    /// <c>v + w·t + u × t</c>. Expects a unit quaternion: any other quaternion gives neither a rotation nor a scaled rotation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d Transform(Vector3d value, Quaterniond rotation)
    {
        var u = new Vector3d(rotation.X, rotation.Y, rotation.Z);
        var t = Cross(u, value) * 2.0;
        return value + t * rotation.W + Cross(u, t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d operator +(Vector3d left, Vector3d right)
    {
        return new Vector3d(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d operator -(Vector3d left, Vector3d right)
    {
        return new Vector3d(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
    }

    /// <summary>IEEE negation: flips the sign bit of every component, so +0 becomes -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d operator -(Vector3d value)
    {
        return new Vector3d(-value.X, -value.Y, -value.Z);
    }

    /// <summary>Component-wise product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d operator *(Vector3d left, Vector3d right)
    {
        return new Vector3d(left.X * right.X, left.Y * right.Y, left.Z * right.Z);
    }

    /// <summary>Component-wise quotient.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d operator /(Vector3d left, Vector3d right)
    {
        return new Vector3d(left.X / right.X, left.Y / right.Y, left.Z / right.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d operator *(Vector3d left, double right)
    {
        return new Vector3d(left.X * right, left.Y * right, left.Z * right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d operator *(double left, Vector3d right)
    {
        return new Vector3d(left * right.X, left * right.Y, left * right.Z);
    }

    /// <summary>Each component divided by <paramref name="right"/> (a true division, not a multiply by the reciprocal).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d operator /(Vector3d left, double right)
    {
        return new Vector3d(left.X / right, left.Y / right, left.Z / right);
    }

    /// <summary>IEEE comparison per component: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector3d left, Vector3d right)
    {
        return left.X == right.X && left.Y == right.Y && left.Z == right.Z;
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector3d left, Vector3d right)
    {
        return !(left == right);
    }

    /// <summary>Exact: every float is a double.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector3d(System.Numerics.Vector3 value)
    {
        return new Vector3d(value.X, value.Y, value.Z);
    }

    /// <summary>Each component rounded to the nearest float.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator System.Numerics.Vector3(Vector3d value)
    {
        return new System.Numerics.Vector3((float)value.X, (float)value.Y, (float)value.Z);
    }

    /// <summary><see cref="double.Equals(double)"/> per component: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Vector3d other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Vector3d other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z);
    }

    /// <summary>Invariant culture, shortest round-trip form: <c>(1.5, 2, -3)</c>.</summary>
    public override readonly string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"({X}, {Y}, {Z})");
    }
}
