using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace CoolEngine.Mathematics;

/// <summary>
/// A float 4D vector: X, Y, Z, W in 16 bytes (the layout of <see cref="System.Numerics.Vector4"/>). Arithmetic runs on
/// <see cref="Vector128{T}"/>, which holds the vector exactly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Vector4 : IEquatable<Vector4>
{
    public float X;
    public float Y;
    public float Z;
    public float W;

    public const int SizeInBytes = 16;

    /// <summary>Every component set to <paramref name="value"/>.</summary>
    public Vector4(float value)
    {
        X = value;
        Y = value;
        Z = value;
        W = value;
    }

    public Vector4(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public Vector4(Vector2 xy, float z, float w)
    {
        X = xy.X;
        Y = xy.Y;
        Z = z;
        W = w;
    }

    public Vector4(Vector3 xyz, float w)
    {
        X = xyz.X;
        Y = xyz.Y;
        Z = xyz.Z;
        W = w;
    }

    public static Vector4 Zero => default;

    public static Vector4 One => new(1f);

    public static Vector4 UnitX => new(1f, 0f, 0f, 0f);

    public static Vector4 UnitY => new(0f, 1f, 0f, 0f);

    public static Vector4 UnitZ => new(0f, 0f, 1f, 0f);

    public static Vector4 UnitW => new(0f, 0f, 0f, 1f);

    /// <summary>0-based: X, Y, Z, W. Throws <see cref="ArgumentOutOfRangeException"/> outside 0 … 3.</summary>
    public float this[int index]
    {
        readonly get => index switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            3 => W,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "A Vector4 index is 0, 1, 2 or 3."),
        };
        set
        {
            switch (index)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                case 2: Z = value; break;
                case 3: W = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), index, "A Vector4 index is 0, 1, 2 or 3.");
            }
        }
    }

    public Vector2 Xy
    {
        readonly get => new(X, Y);
        set
        {
            X = value.X;
            Y = value.Y;
        }
    }

    public Vector3 Xyz
    {
        readonly get => new(X, Y, Z);
        set
        {
            X = value.X;
            Y = value.Y;
            Z = value.Z;
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
    public readonly Vector4 Normalized()
    {
        var v = Simd.Load(this);
        return Simd.ToVector4(v / MathF.Sqrt(Vector128.Dot(v, v)));
    }

    /// <summary>Normalizes in place; see <see cref="Normalized"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Normalize()
    {
        this = Normalized();
    }

    /// <inheritdoc cref="Normalized"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Normalize(Vector4 vector)
    {
        return vector.Normalized();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector4 left, Vector4 right)
    {
        return Vector128.Dot(Simd.Load(left), Simd.Load(right));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector4 a, Vector4 b)
    {
        var d = Simd.Load(a) - Simd.Load(b);
        return MathF.Sqrt(Vector128.Dot(d, d));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vector4 a, Vector4 b)
    {
        var d = Simd.Load(a) - Simd.Load(b);
        return Vector128.Dot(d, d);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ComponentMin(Vector4 a, Vector4 b)
    {
        return Simd.ToVector4(Vector128.Min(Simd.Load(a), Simd.Load(b)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 ComponentMax(Vector4 a, Vector4 b)
    {
        return Simd.ToVector4(Vector128.Max(Simd.Load(a), Simd.Load(b)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Abs(Vector4 vector)
    {
        return Simd.ToVector4(Vector128.Abs(Simd.Load(vector)));
    }

    /// <summary><c>ComponentMin(ComponentMax(value, min), max)</c>; no check that <c>min &lt;= max</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Clamp(Vector4 value, Vector4 min, Vector4 max)
    {
        return Simd.ToVector4(Vector128.Min(Vector128.Max(Simd.Load(value), Simd.Load(min)), Simd.Load(max)));
    }

    /// <summary><c>a + (b - a) * t</c>; <paramref name="t"/> is not clamped, so values outside [0, 1] extrapolate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Lerp(Vector4 a, Vector4 b, float t)
    {
        var va = Simd.Load(a);
        return Simd.ToVector4(va + (Simd.Load(b) - va) * t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator +(Vector4 left, Vector4 right)
    {
        return Simd.ToVector4(Simd.Load(left) + Simd.Load(right));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator -(Vector4 left, Vector4 right)
    {
        return Simd.ToVector4(Simd.Load(left) - Simd.Load(right));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator -(Vector4 vector)
    {
        return Simd.ToVector4(Simd.Negate(Simd.Load(vector)));
    }

    /// <summary>Component-wise product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator *(Vector4 left, Vector4 right)
    {
        return Simd.ToVector4(Simd.Load(left) * Simd.Load(right));
    }

    /// <summary>Component-wise quotient.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator /(Vector4 left, Vector4 right)
    {
        return Simd.ToVector4(Simd.Load(left) / Simd.Load(right));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator *(Vector4 vector, float scale)
    {
        return Simd.ToVector4(Simd.Load(vector) * scale);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator *(float scale, Vector4 vector)
    {
        return Simd.ToVector4(scale * Simd.Load(vector));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator /(Vector4 vector, float divisor)
    {
        return Simd.ToVector4(Simd.Load(vector) / divisor);
    }

    /// <summary>IEEE comparison per component: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector4 left, Vector4 right)
    {
        return Vector128.EqualsAll(Simd.Load(left), Simd.Load(right));
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector4 left, Vector4 right)
    {
        return !(left == right);
    }

    /// <summary>Exact, same layout.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator System.Numerics.Vector4(Vector4 value)
    {
        return Unsafe.BitCast<Vector4, System.Numerics.Vector4>(value);
    }

    /// <summary>Exact, same layout.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector4(System.Numerics.Vector4 value)
    {
        return Unsafe.BitCast<System.Numerics.Vector4, Vector4>(value);
    }

    /// <summary><see cref="float.Equals(float)"/> per component: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Vector4 other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Vector4 other && Equals(other);
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
