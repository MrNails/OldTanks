using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace CoolEngine.Mathematics;

/// <summary>
/// A float quaternion: vector part X, Y, Z, then scalar W, in 16 bytes (the layout of
/// <see cref="System.Numerics.Quaternion"/>). A unit quaternion is a rotation; <c>a * b</c> is the Hamilton product,
/// which rotates by <c>b</c> first, then <c>a</c>. Angles in radians, right-handed. There are no public <c>+</c>,
/// <c>-</c> or scalar operators.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Quaternion : IEquatable<Quaternion>
{
    public float X;
    public float Y;
    public float Z;
    public float W;

    public const int SizeInBytes = 16;

    public Quaternion(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>The vector part <paramref name="xyz"/> and the scalar part <paramref name="w"/>.</summary>
    public Quaternion(Vector3 xyz, float w)
    {
        X = xyz.X;
        Y = xyz.Y;
        Z = xyz.Z;
        W = w;
    }

    /// <summary>(0, 0, 0, 1): no rotation.</summary>
    public static Quaternion Identity => new(0f, 0f, 0f, 1f);

    /// <summary>The vector part (X, Y, Z).</summary>
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
            var v = Load(this);
            return MathF.Sqrt(Vector128.Dot(v, v));
        }
    }

    public readonly float LengthSquared
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            var v = Load(this);
            return Vector128.Dot(v, v);
        }
    }

    /// <summary>This quaternion divided by its length. The zero quaternion gives NaN in every component (no check).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Quaternion Normalized()
    {
        var v = Load(this);
        return Store(v / MathF.Sqrt(Vector128.Dot(v, v)));
    }

    /// <summary>Normalizes in place; see <see cref="Normalized"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Normalize()
    {
        this = Normalized();
    }

    /// <summary>(-X, -Y, -Z, W): the inverse rotation of a unit quaternion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Quaternion Conjugated()
    {
        return Store(Load(this) ^ Vector128.Create(float.NegativeZero, float.NegativeZero, float.NegativeZero, 0f));
    }

    /// <summary>The multiplicative inverse, <see cref="Conjugated"/> / <see cref="LengthSquared"/>; zero gives NaN (no check).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Quaternion Inverted()
    {
        var v = Load(this);
        var conjugate = v ^ Vector128.Create(float.NegativeZero, float.NegativeZero, float.NegativeZero, 0f);
        return Store(conjugate / Vector128.Dot(v, v));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Dot(Quaternion a, Quaternion b)
    {
        return Vector128.Dot(Load(a), Load(b));
    }

    /// <summary>
    /// The rotation by <paramref name="angle"/> radians about <paramref name="axis"/>, counterclockwise when looking from
    /// the axis' tip toward the origin: <c>(normalize(axis) * sin(angle / 2), cos(angle / 2))</c>. The axis is
    /// normalized here; a zero axis (<c>LengthSquared == 0</c>) gives <see cref="Identity"/>.
    /// </summary>
    public static Quaternion FromAxisAngle(Vector3 axis, float angle)
    {
        var lengthSquared = axis.LengthSquared;
        if (lengthSquared == 0f)
            return Identity;

        var (sin, cos) = MathF.SinCos(angle * 0.5f);
        return new Quaternion(axis / MathF.Sqrt(lengthSquared) * sin, cos);
    }

    /// <summary>
    /// Spherical interpolation from <paramref name="a"/> (<c>t = 0</c>) to <paramref name="b"/> (<c>t = 1</c>) along the
    /// shortest arc: when <c>Dot(a, b) &lt; 0</c>, <paramref name="b"/> is negated (the same rotation). When the two are
    /// nearly equal (<c>Dot &gt; 1 - 1e-6</c>) it returns the normalized linear interpolation
    /// <c>normalize(a + (b - a) * t)</c>; otherwise, with <c>θ = acos(Dot)</c>,
    /// <c>(sin((1 - t)θ) a + sin(tθ) b) / sin θ</c>. Expects unit quaternions; <paramref name="t"/> is not clamped.
    /// </summary>
    public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
    {
        var va = Load(a);
        var vb = Load(b);
        var dot = Vector128.Dot(va, vb);
        if (dot < 0f)
        {
            vb = Simd.Negate(vb);
            dot = -dot;
        }

        if (dot > 1f - 1e-6f)
        {
            var lerp = va + (vb - va) * t;
            return Store(lerp / MathF.Sqrt(Vector128.Dot(lerp, lerp)));
        }

        var theta = MathF.Acos(dot);
        var weightA = MathF.Sin((1f - t) * theta);
        var weightB = MathF.Sin(t * theta);
        return Store((va * weightA + vb * weightB) / MathF.Sin(theta));
    }

    /// <summary>
    /// The Hamilton product: <c>(a * b)</c> rotates by <paramref name="b"/> first, then by <paramref name="a"/>.
    /// <c>x = aw bx + ax bw + ay bz - az by</c>, <c>y = aw by - ax bz + ay bw + az bx</c>,
    /// <c>z = aw bz + ax by - ay bx + az bw</c>, <c>w = aw bw - ax bx - ay by - az bz</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaternion operator *(Quaternion a, Quaternion b)
    {
        var vb = Load(b);
        var negativeZero = float.NegativeZero;

        // b's lanes rearranged and sign-flipped so that each of a's components multiplies one vector:
        // a.x * (bw, -bz, by, -bx) + a.y * (bz, bw, -bx, -by) + a.z * (-by, bx, bw, -bz) + a.w * (bx, by, bz, bw).
        var forX = Vector128.Shuffle(vb, Vector128.Create(3, 2, 1, 0)) ^ Vector128.Create(0f, negativeZero, 0f, negativeZero);
        var forY = Vector128.Shuffle(vb, Vector128.Create(2, 3, 0, 1)) ^ Vector128.Create(0f, 0f, negativeZero, negativeZero);
        var forZ = Vector128.Shuffle(vb, Vector128.Create(1, 0, 3, 2)) ^ Vector128.Create(negativeZero, 0f, 0f, negativeZero);

        var result = vb * a.W;
        result = Vector128.MultiplyAddEstimate(forX, Vector128.Create(a.X), result);
        result = Vector128.MultiplyAddEstimate(forY, Vector128.Create(a.Y), result);
        result = Vector128.MultiplyAddEstimate(forZ, Vector128.Create(a.Z), result);
        return Store(result);
    }

    /// <summary>
    /// Rotates <paramref name="vector"/> by the unit quaternion <paramref name="rotation"/>:
    /// <c>v + 2w(u × v) + 2u × (u × v)</c> with <c>u</c> = <see cref="Xyz"/>, computed as <c>t = 2(u × v)</c>,
    /// <c>v + w t + u × t</c>. A non-unit quaternion also scales the result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator *(Quaternion rotation, Vector3 vector)
    {
        var u = rotation.Xyz;
        var t = Vector3.Cross(u, vector) * 2f;
        return vector + t * rotation.W + Vector3.Cross(u, t);
    }

    /// <summary>IEEE comparison per component: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Quaternion left, Quaternion right)
    {
        return Vector128.EqualsAll(Load(left), Load(right));
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Quaternion left, Quaternion right)
    {
        return !(left == right);
    }

    /// <summary>Exact, same layout and the same Hamilton product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator System.Numerics.Quaternion(Quaternion value)
    {
        return Unsafe.BitCast<Quaternion, System.Numerics.Quaternion>(value);
    }

    /// <summary>Exact, same layout and the same Hamilton product.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Quaternion(System.Numerics.Quaternion value)
    {
        return Unsafe.BitCast<System.Numerics.Quaternion, Quaternion>(value);
    }

    /// <summary><see cref="float.Equals(float)"/> per component: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Quaternion other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Quaternion other && Equals(other);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(X, Y, Z, W);
    }

    /// <summary>Invariant culture, shortest round-trip form: <c>(x, y, z, w)</c>.</summary>
    public override readonly string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"({X}, {Y}, {Z}, {W})");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> Load(Quaternion value)
    {
        return Unsafe.BitCast<Quaternion, Vector128<float>>(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Quaternion Store(Vector128<float> vector)
    {
        return Unsafe.BitCast<Vector128<float>, Quaternion>(vector);
    }
}
