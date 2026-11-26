using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CoolEngine.Mathematics;

/// <summary>
/// A double 4D vector: X, Y, Z, W in 32 bytes, with the API shape of <see cref="System.Numerics.Vector4"/> (its float twin)
/// and the same semantics: <see cref="Min"/> and <see cref="Max"/> propagate NaN and order -0 below +0, unary minus flips
/// the sign bit, <see cref="Normalize"/> of zero gives NaN. Own code, scalar on the fields, with no fused multiply-add and
/// every sum left to right. Bit stability: every member (they use only + − × ÷, sqrt and exact operations: negation,
/// Abs, Min, Max, conversions) gives the same bits on every x64 CPU and instruction set for finite inputs (a NaN's sign
/// and payload are not covered).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Vector4d : IEquatable<Vector4d>
{
    public double X;
    public double Y;
    public double Z;
    public double W;

    public const int SizeInBytes = 32;

    /// <summary>Every component set to <paramref name="value"/>.</summary>
    public Vector4d(double value)
    {
        X = value;
        Y = value;
        Z = value;
        W = value;
    }

    public Vector4d(double x, double y, double z, double w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public Vector4d(Vector2d xy, double z, double w)
    {
        X = xy.X;
        Y = xy.Y;
        Z = z;
        W = w;
    }

    public Vector4d(Vector3d xyz, double w)
    {
        X = xyz.X;
        Y = xyz.Y;
        Z = xyz.Z;
        W = w;
    }

    public static Vector4d Zero => default;

    public static Vector4d One => new(1.0);

    public static Vector4d UnitX => new(1.0, 0.0, 0.0, 0.0);

    public static Vector4d UnitY => new(0.0, 1.0, 0.0, 0.0);

    public static Vector4d UnitZ => new(0.0, 0.0, 1.0, 0.0);

    public static Vector4d UnitW => new(0.0, 0.0, 0.0, 1.0);

    /// <summary>0-based: X, Y, Z, W. Throws <see cref="ArgumentOutOfRangeException"/> outside 0 … 3.</summary>
    public double this[int index]
    {
        readonly get => index switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            3 => W,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "A Vector4d index is 0, 1, 2 or 3."),
        };
        set
        {
            switch (index)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                case 2: Z = value; break;
                case 3: W = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), index, "A Vector4d index is 0, 1, 2 or 3.");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double Length()
    {
        return Math.Sqrt(LengthSquared());
    }

    /// <summary><c>X·X + Y·Y + Z·Z + W·W</c>, left to right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double LengthSquared()
    {
        return X * X + Y * Y + Z * Z + W * W;
    }

    /// <summary>Component-wise <see cref="Math.Abs(double)"/> (clears the sign bit).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d Abs(Vector4d value)
    {
        return new Vector4d(Math.Abs(value.X), Math.Abs(value.Y), Math.Abs(value.Z), Math.Abs(value.W));
    }

    /// <summary><c>Min(Max(value, min), max)</c>, as System.Numerics; no check that <c>min &lt;= max</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d Clamp(Vector4d value, Vector4d min, Vector4d max)
    {
        return Min(Max(value, min), max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Distance(Vector4d value1, Vector4d value2)
    {
        return (value1 - value2).Length();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DistanceSquared(Vector4d value1, Vector4d value2)
    {
        return (value1 - value2).LengthSquared();
    }

    /// <summary><c>l.X·r.X + l.Y·r.Y + l.Z·r.Z + l.W·r.W</c>, left to right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Dot(Vector4d left, Vector4d right)
    {
        return left.X * right.X + left.Y * right.Y + left.Z * right.Z + left.W * right.W;
    }

    /// <summary><c>value1 + (value2 - value1) * amount</c>; <paramref name="amount"/> is not clamped, so values outside [0, 1] extrapolate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d Lerp(Vector4d value1, Vector4d value2, double amount)
    {
        return value1 + (value2 - value1) * amount;
    }

    /// <summary>Component-wise <see cref="Math.Max(double, double)"/>: NaN propagates, +0 is above -0 (System.Numerics' semantics).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d Max(Vector4d value1, Vector4d value2)
    {
        return new Vector4d(Math.Max(value1.X, value2.X), Math.Max(value1.Y, value2.Y), Math.Max(value1.Z, value2.Z),
            Math.Max(value1.W, value2.W));
    }

    /// <summary>Component-wise <see cref="Math.Min(double, double)"/>: NaN propagates, -0 is below +0 (System.Numerics' semantics).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d Min(Vector4d value1, Vector4d value2)
    {
        return new Vector4d(Math.Min(value1.X, value2.X), Math.Min(value1.Y, value2.Y), Math.Min(value1.Z, value2.Z),
            Math.Min(value1.W, value2.W));
    }

    /// <summary>
    /// The vector times the reciprocal of its length (<c>1 / Length()</c> once, then a multiply per component): at most
    /// 1 ulp from a true division per component. The zero vector gives NaN in every component (1 / 0 = +∞, 0 · ∞ = NaN; no check).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d Normalize(Vector4d value)
    {
        return value * (1.0 / value.Length());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d operator +(Vector4d left, Vector4d right)
    {
        return new Vector4d(left.X + right.X, left.Y + right.Y, left.Z + right.Z, left.W + right.W);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d operator -(Vector4d left, Vector4d right)
    {
        return new Vector4d(left.X - right.X, left.Y - right.Y, left.Z - right.Z, left.W - right.W);
    }

    /// <summary>IEEE negation: flips the sign bit of every component, so +0 becomes -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d operator -(Vector4d value)
    {
        return new Vector4d(-value.X, -value.Y, -value.Z, -value.W);
    }

    /// <summary>Component-wise product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d operator *(Vector4d left, Vector4d right)
    {
        return new Vector4d(left.X * right.X, left.Y * right.Y, left.Z * right.Z, left.W * right.W);
    }

    /// <summary>Component-wise quotient.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d operator /(Vector4d left, Vector4d right)
    {
        return new Vector4d(left.X / right.X, left.Y / right.Y, left.Z / right.Z, left.W / right.W);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d operator *(Vector4d left, double right)
    {
        return new Vector4d(left.X * right, left.Y * right, left.Z * right, left.W * right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d operator *(double left, Vector4d right)
    {
        return new Vector4d(left * right.X, left * right.Y, left * right.Z, left * right.W);
    }

    /// <summary>Each component divided by <paramref name="right"/> (a true division, not a multiply by the reciprocal).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d operator /(Vector4d left, double right)
    {
        return new Vector4d(left.X / right, left.Y / right, left.Z / right, left.W / right);
    }

    /// <summary>IEEE comparison per component: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector4d left, Vector4d right)
    {
        return left.X == right.X && left.Y == right.Y && left.Z == right.Z && left.W == right.W;
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector4d left, Vector4d right)
    {
        return !(left == right);
    }

    /// <summary>Exact: every float is a double.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector4d(System.Numerics.Vector4 value)
    {
        return new Vector4d(value.X, value.Y, value.Z, value.W);
    }

    /// <summary>Each component rounded to the nearest float.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator System.Numerics.Vector4(Vector4d value)
    {
        return new System.Numerics.Vector4((float)value.X, (float)value.Y, (float)value.Z, (float)value.W);
    }

    /// <summary><see cref="double.Equals(double)"/> per component: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Vector4d other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Vector4d other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z, W);
    }

    /// <summary>Invariant culture, shortest round-trip form: <c>(1.5, 2, -3, 0.25)</c>.</summary>
    public override readonly string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"({X}, {Y}, {Z}, {W})");
    }
}
