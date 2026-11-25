using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace CoolEngine.Mathematics;

/// <summary>
/// A float 3D vector: X, Y, Z packed in 12 bytes with no padding (the layout of <see cref="System.Numerics.Vector3"/>,
/// so vertex structs keep their size). Arithmetic runs on <see cref="Vector128{T}"/> with the fourth lane zero.
/// Right-handed: <c>Cross(UnitX, UnitY) == UnitZ</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Vector3 : IEquatable<Vector3>
{
    public float X;
    public float Y;
    public float Z;

    public const int SizeInBytes = 12;

    /// <summary>Every component set to <paramref name="value"/>.</summary>
    public Vector3(float value)
    {
        X = value;
        Y = value;
        Z = value;
    }

    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public Vector3(Vector2 xy, float z)
    {
        X = xy.X;
        Y = xy.Y;
        Z = z;
    }

    public static Vector3 Zero => default;

    public static Vector3 One => new(1f);

    public static Vector3 UnitX => new(1f, 0f, 0f);

    public static Vector3 UnitY => new(0f, 1f, 0f);

    public static Vector3 UnitZ => new(0f, 0f, 1f);

    /// <summary>0-based: X, Y, Z. Throws <see cref="ArgumentOutOfRangeException"/> outside 0 … 2.</summary>
    public float this[int index]
    {
        readonly get => index switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "A Vector3 index is 0, 1 or 2."),
        };
        set
        {
            switch (index)
            {
                case 0: X = value; break;
                case 1: Y = value; break;
                case 2: Z = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(index), index, "A Vector3 index is 0, 1 or 2.");
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
    public readonly Vector3 Normalized()
    {
        var v = Simd.Load(this);
        return Simd.ToVector3(v / MathF.Sqrt(Vector128.Dot(v, v)));
    }

    /// <summary>Normalizes in place; see <see cref="Normalized"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Normalize()
    {
        this = Normalized();
    }

    /// <inheritdoc cref="Normalized"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Normalize(Vector3 vector)
    {
        return vector.Normalized();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Vector3 left, Vector3 right)
    {
        return Vector128.Dot(Simd.Load(left), Simd.Load(right));
    }

    /// <summary>The right-handed cross product: <c>Cross(UnitX, UnitY) == UnitZ</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Cross(Vector3 left, Vector3 right)
    {
        var l = Simd.Load(left);
        var r = Simd.Load(right);

        // (l.y r.z - l.z r.y, l.z r.x - l.x r.z, l.x r.y - l.y r.x, 0)
        var lYzx = Vector128.Shuffle(l, Vector128.Create(1, 2, 0, 3));
        var rZxy = Vector128.Shuffle(r, Vector128.Create(2, 0, 1, 3));
        var lZxy = Vector128.Shuffle(l, Vector128.Create(2, 0, 1, 3));
        var rYzx = Vector128.Shuffle(r, Vector128.Create(1, 2, 0, 3));

        return Simd.ToVector3(lYzx * rZxy - lZxy * rYzx);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Distance(Vector3 a, Vector3 b)
    {
        var d = Simd.Load(a) - Simd.Load(b);
        return MathF.Sqrt(Vector128.Dot(d, d));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float DistanceSquared(Vector3 a, Vector3 b)
    {
        var d = Simd.Load(a) - Simd.Load(b);
        return Vector128.Dot(d, d);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ComponentMin(Vector3 a, Vector3 b)
    {
        return Simd.ToVector3(Vector128.Min(Simd.Load(a), Simd.Load(b)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ComponentMax(Vector3 a, Vector3 b)
    {
        return Simd.ToVector3(Vector128.Max(Simd.Load(a), Simd.Load(b)));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Abs(Vector3 vector)
    {
        return Simd.ToVector3(Vector128.Abs(Simd.Load(vector)));
    }

    /// <summary><c>ComponentMin(ComponentMax(value, min), max)</c>; no check that <c>min &lt;= max</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Clamp(Vector3 value, Vector3 min, Vector3 max)
    {
        return Simd.ToVector3(Vector128.Min(Vector128.Max(Simd.Load(value), Simd.Load(min)), Simd.Load(max)));
    }

    /// <summary><c>a + (b - a) * t</c>; <paramref name="t"/> is not clamped, so values outside [0, 1] extrapolate.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
    {
        var va = Simd.Load(a);
        return Simd.ToVector3(va + (Simd.Load(b) - va) * t);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator +(Vector3 left, Vector3 right)
    {
        return Simd.ToVector3(Simd.Load(left) + Simd.Load(right));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator -(Vector3 left, Vector3 right)
    {
        return Simd.ToVector3(Simd.Load(left) - Simd.Load(right));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator -(Vector3 vector)
    {
        return Simd.ToVector3(Simd.Negate(Simd.Load(vector)));
    }

    /// <summary>Component-wise product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator *(Vector3 left, Vector3 right)
    {
        return Simd.ToVector3(Simd.Load(left) * Simd.Load(right));
    }

    /// <summary>Component-wise quotient.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator /(Vector3 left, Vector3 right)
    {
        return Simd.ToVector3(Simd.Load(left) / Simd.Load(right));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator *(Vector3 vector, float scale)
    {
        return Simd.ToVector3(Simd.Load(vector) * scale);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator *(float scale, Vector3 vector)
    {
        return Simd.ToVector3(scale * Simd.Load(vector));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator /(Vector3 vector, float divisor)
    {
        return Simd.ToVector3(Simd.Load(vector) / divisor);
    }

    /// <summary>IEEE comparison per component: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Vector3 left, Vector3 right)
    {
        return Vector128.EqualsAll(Simd.Load(left), Simd.Load(right));
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Vector3 left, Vector3 right)
    {
        return !(left == right);
    }

    /// <summary>Exact, same layout.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator System.Numerics.Vector3(Vector3 value)
    {
        return Unsafe.BitCast<Vector3, System.Numerics.Vector3>(value);
    }

    /// <summary>Exact, same layout.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector3(System.Numerics.Vector3 value)
    {
        return Unsafe.BitCast<System.Numerics.Vector3, Vector3>(value);
    }

    /// <summary><see cref="float.Equals(float)"/> per component: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Vector3 other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Vector3 other && Equals(other);
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
