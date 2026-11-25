using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace CoolEngine.Mathematics;

/// <summary>
/// A float 2D vector: X, Y in 8 bytes (the layout of <see cref="System.Numerics.Vector2"/>). Arithmetic runs on
/// <see cref="Vector128{T}"/>: the 8 bytes load as one 64-bit lane, the upper two float lanes zero.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Vector2 : IEquatable<Vector2>
{
    public float X;
    public float Y;

    public const int SizeInBytes = 8;

    /// <summary>Every component set to <paramref name="value"/>.</summary>
    public Vector2(float value)
    {
        X = value;
        Y = value;
    }

    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static Vector2 Zero => default;

    public static Vector2 One => new(1f);

    public static Vector2 UnitX => new(1f, 0f);

    public static Vector2 UnitY => new(0f, 1f);

    /// <summary>0-based: X, Y. Throws <see cref="ArgumentOutOfRangeException"/> outside 0 … 1.</summary>
    public float this[int index]
    {
        readonly get => index switch
        {
            0 => X,
            1 => Y,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "A Vector2 index is 0 or 1."),
        };
        set
        {
            switch (index)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), index, "A Vector2 index is 0 or 1.");
            }
        }
    }

    public readonly float Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var v = Simd.Load(this);
            return MathF.Sqrt(Vector128.Dot(v, v));
        }
    }

    public readonly float LengthSquared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var v = Simd.Load(this);
            return Vector128.Dot(v, v);
        }
    }

    /// <summary>This vector divided by its length. The zero vector gives NaN in every component (no check).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector2 Normalized()
    {
        var v = Simd.Load(this);
        return Simd.ToVector2(v / MathF.Sqrt(Vector128.Dot(v, v)));
    }

    /// <summary>Normalizes in place; see <see cref="Normalized"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Normalize()
    {
        this = Normalized();
    }

    /// <inheritdoc cref="Normalized"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Normalize(Vector2 vector)
    {
        return vector.Normalized();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector2 left, Vector2 right)
    {
        return Vector128.Dot(Simd.Load(left), Simd.Load(right));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector2 a, Vector2 b)
    {
        var d = Simd.Load(a) - Simd.Load(b);
        return MathF.Sqrt(Vector128.Dot(d, d));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vector2 a, Vector2 b)
    {
        var d = Simd.Load(a) - Simd.Load(b);
        return Vector128.Dot(d, d);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ComponentMin(Vector2 a, Vector2 b)
    {
        return Simd.ToVector2(Vector128.Min(Simd.Load(a), Simd.Load(b)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 ComponentMax(Vector2 a, Vector2 b)
    {
        return Simd.ToVector2(Vector128.Max(Simd.Load(a), Simd.Load(b)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Abs(Vector2 vector)
    {
        return Simd.ToVector2(Vector128.Abs(Simd.Load(vector)));
    }

    /// <summary><c>ComponentMin(ComponentMax(value, min), max)</c>; no check that <c>min &lt;= max</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Clamp(Vector2 value, Vector2 min, Vector2 max)
    {
        return Simd.ToVector2(Vector128.Min(Vector128.Max(Simd.Load(value), Simd.Load(min)), Simd.Load(max)));
    }

    /// <summary><c>a + (b - a) * t</c>; <paramref name="t"/> is not clamped, so values outside [0, 1] extrapolate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
    {
        var va = Simd.Load(a);
        return Simd.ToVector2(va + (Simd.Load(b) - va) * t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator +(Vector2 left, Vector2 right)
    {
        return Simd.ToVector2(Simd.Load(left) + Simd.Load(right));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator -(Vector2 left, Vector2 right)
    {
        return Simd.ToVector2(Simd.Load(left) - Simd.Load(right));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator -(Vector2 vector)
    {
        return Simd.ToVector2(Simd.Negate(Simd.Load(vector)));
    }

    /// <summary>Component-wise product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator *(Vector2 left, Vector2 right)
    {
        return Simd.ToVector2(Simd.Load(left) * Simd.Load(right));
    }

    /// <summary>Component-wise quotient.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator /(Vector2 left, Vector2 right)
    {
        return Simd.ToVector2(Simd.Load(left) / Simd.Load(right));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator *(Vector2 vector, float scale)
    {
        return Simd.ToVector2(Simd.Load(vector) * scale);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator *(float scale, Vector2 vector)
    {
        return Simd.ToVector2(scale * Simd.Load(vector));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator /(Vector2 vector, float divisor)
    {
        return Simd.ToVector2(Simd.Load(vector) / divisor);
    }

    /// <summary>IEEE comparison per component: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector2 left, Vector2 right)
    {
        return Vector128.EqualsAll(Simd.Load(left), Simd.Load(right));
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector2 left, Vector2 right)
    {
        return !(left == right);
    }

    /// <summary>Exact, same layout.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator System.Numerics.Vector2(Vector2 value)
    {
        return Unsafe.BitCast<Vector2, System.Numerics.Vector2>(value);
    }

    /// <summary>Exact, same layout.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector2(System.Numerics.Vector2 value)
    {
        return Unsafe.BitCast<System.Numerics.Vector2, Vector2>(value);
    }

    /// <summary><see cref="float.Equals(float)"/> per component: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Vector2 other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Vector2 other && Equals(other);
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
