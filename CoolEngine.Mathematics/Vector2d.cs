using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CoolEngine.Mathematics;

/// <summary>
/// A double 2D vector: X, Y in 16 bytes, with the API shape of <see cref="System.Numerics.Vector2"/> (its float twin)
/// and the same semantics: <see cref="Min"/> and <see cref="Max"/> propagate NaN and order -0 below +0, unary minus flips
/// the sign bit, <see cref="Normalize"/> of zero gives NaN. Own code, scalar on the fields, with no fused multiply-add and
/// every sum left to right. Bit stability: every member (they use only + − × ÷ and sqrt) gives the same bits on every
/// x64 CPU and instruction set for finite inputs (a NaN's sign and payload are not covered).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Vector2d : IEquatable<Vector2d>
{
    public double X;
    public double Y;

    public const int SizeInBytes = 16;

    /// <summary>Every component set to <paramref name="value"/>.</summary>
    public Vector2d(double value)
    {
        X = value;
        Y = value;
    }

    public Vector2d(double x, double y)
    {
        X = x;
        Y = y;
    }

    public static Vector2d Zero => default;

    public static Vector2d One => new(1.0);

    public static Vector2d UnitX => new(1.0, 0.0);

    public static Vector2d UnitY => new(0.0, 1.0);

    /// <summary>0-based: X, Y. Throws <see cref="ArgumentOutOfRangeException"/> outside 0 … 1.</summary>
    public double this[int index]
    {
        readonly get => index switch
        {
            0 => X,
            1 => Y,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "A Vector2d index is 0 or 1."),
        };
        set
        {
            switch (index)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), index, "A Vector2d index is 0 or 1.");
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double Length()
    {
        return Math.Sqrt(LengthSquared());
    }

    /// <summary><c>X·X + Y·Y</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly double LengthSquared()
    {
        return X * X + Y * Y;
    }

    /// <summary>Component-wise <see cref="Math.Abs(double)"/> (clears the sign bit).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d Abs(Vector2d value)
    {
        return new Vector2d(Math.Abs(value.X), Math.Abs(value.Y));
    }

    /// <summary><c>Min(Max(value, min), max)</c>, as System.Numerics; no check that <c>min &lt;= max</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d Clamp(Vector2d value, Vector2d min, Vector2d max)
    {
        return Min(Max(value, min), max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Distance(Vector2d value1, Vector2d value2)
    {
        return (value1 - value2).Length();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DistanceSquared(Vector2d value1, Vector2d value2)
    {
        return (value1 - value2).LengthSquared();
    }

    /// <summary><c>l.X·r.X + l.Y·r.Y</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Dot(Vector2d left, Vector2d right)
    {
        return left.X * right.X + left.Y * right.Y;
    }

    /// <summary><c>value1 + (value2 - value1) * amount</c>; <paramref name="amount"/> is not clamped, so values outside [0, 1] extrapolate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d Lerp(Vector2d value1, Vector2d value2, double amount)
    {
        return value1 + (value2 - value1) * amount;
    }

    /// <summary>Component-wise <see cref="Math.Max(double, double)"/>: NaN propagates, +0 is above -0 (System.Numerics' semantics).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d Max(Vector2d value1, Vector2d value2)
    {
        return new Vector2d(Math.Max(value1.X, value2.X), Math.Max(value1.Y, value2.Y));
    }

    /// <summary>Component-wise <see cref="Math.Min(double, double)"/>: NaN propagates, -0 is below +0 (System.Numerics' semantics).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d Min(Vector2d value1, Vector2d value2)
    {
        return new Vector2d(Math.Min(value1.X, value2.X), Math.Min(value1.Y, value2.Y));
    }

    /// <summary>
    /// The vector times the reciprocal of its length (<c>1 / Length()</c> once, then a multiply per component): at most
    /// 1 ulp from a true division per component. The zero vector gives NaN in every component (1 / 0 = +∞, 0 · ∞ = NaN; no check).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d Normalize(Vector2d value)
    {
        return value * (1.0 / value.Length());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d operator +(Vector2d left, Vector2d right)
    {
        return new Vector2d(left.X + right.X, left.Y + right.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d operator -(Vector2d left, Vector2d right)
    {
        return new Vector2d(left.X - right.X, left.Y - right.Y);
    }

    /// <summary>IEEE negation: flips the sign bit of every component, so +0 becomes -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d operator -(Vector2d value)
    {
        return new Vector2d(-value.X, -value.Y);
    }

    /// <summary>Component-wise product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d operator *(Vector2d left, Vector2d right)
    {
        return new Vector2d(left.X * right.X, left.Y * right.Y);
    }

    /// <summary>Component-wise quotient.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d operator /(Vector2d left, Vector2d right)
    {
        return new Vector2d(left.X / right.X, left.Y / right.Y);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d operator *(Vector2d left, double right)
    {
        return new Vector2d(left.X * right, left.Y * right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d operator *(double left, Vector2d right)
    {
        return new Vector2d(left * right.X, left * right.Y);
    }

    /// <summary>Each component divided by <paramref name="right"/> (a true division, not a multiply by the reciprocal).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d operator /(Vector2d left, double right)
    {
        return new Vector2d(left.X / right, left.Y / right);
    }

    /// <summary>IEEE comparison per component: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector2d left, Vector2d right)
    {
        return left.X == right.X && left.Y == right.Y;
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector2d left, Vector2d right)
    {
        return !(left == right);
    }

    /// <summary>Exact: every float is a double.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector2d(System.Numerics.Vector2 value)
    {
        return new Vector2d(value.X, value.Y);
    }

    /// <summary>Each component rounded to the nearest float.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator System.Numerics.Vector2(Vector2d value)
    {
        return new System.Numerics.Vector2((float)value.X, (float)value.Y);
    }

    /// <summary><see cref="double.Equals(double)"/> per component: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Vector2d other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Vector2d other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(X, Y);
    }

    /// <summary>Invariant culture, shortest round-trip form: <c>(1.5, 2)</c>.</summary>
    public override readonly string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"({X}, {Y})");
    }
}
