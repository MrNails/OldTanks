using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CoolEngine.Mathematics;

/// <summary>
/// A double quaternion: vector part X, Y, Z, then scalar W, in 32 bytes, with the API shape of
/// <see cref="System.Numerics.Quaternion"/> (its float twin). A unit quaternion is a rotation; <c>a * b</c> is the Hamilton
/// product, which rotates by <c>b</c> first, then <c>a</c>; <see cref="Vector3d.Transform"/> rotates a vector. Own code,
/// scalar on the fields, with no fused multiply-add and every sum left to right. Bit stability: every member that uses only
/// + − × ÷ and sqrt gives the same bits on every x64 CPU and instruction set for finite inputs (a NaN's sign and payload
/// are not covered); <see cref="CreateFromAxisAngle"/> and <see cref="Slerp"/> call sin, cos or acos, follow the OS math
/// library and say so. Angles in radians, right-handed.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Quaterniond : IEquatable<Quaterniond>
{
    public double X;
    public double Y;
    public double Z;
    public double W;

    public const int SizeInBytes = 32;

    public Quaterniond(double x, double y, double z, double w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>The vector part <paramref name="vectorPart"/> and the scalar part <paramref name="scalarPart"/>.</summary>
    public Quaterniond(Vector3d vectorPart, double scalarPart)
    {
        X = vectorPart.X;
        Y = vectorPart.Y;
        Z = vectorPart.Z;
        W = scalarPart;
    }

    /// <summary>(0, 0, 0, 1): no rotation.</summary>
    public static Quaterniond Identity => new(0.0, 0.0, 0.0, 1.0);

    /// <summary>(0, 0, 0, 0).</summary>
    public static Quaterniond Zero => default;

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

    /// <summary>
    /// The rotation by <paramref name="angle"/> radians about the unit vector <paramref name="axis"/>, counterclockwise when
    /// looking from the axis' tip toward the origin: <c>(axis · sin(angle / 2), cos(angle / 2))</c>. Like System.Numerics,
    /// the axis is expected to be normalized and is not normalized here. Calls sin and cos, so the result follows the OS
    /// math library and is not promised bit-identical across CPUs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaterniond CreateFromAxisAngle(Vector3d axis, double angle)
    {
        var (sin, cos) = Math.SinCos(angle * 0.5);
        return new Quaterniond(axis.X * sin, axis.Y * sin, axis.Z * sin, cos);
    }

    /// <summary>(-X, -Y, -Z, W): the inverse rotation of a unit quaternion.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaterniond Conjugate(Quaterniond value)
    {
        return new Quaterniond(-value.X, -value.Y, -value.Z, value.W);
    }

    /// <summary>The multiplicative inverse, the conjugate divided by <see cref="LengthSquared"/>; zero gives NaN (no check).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaterniond Inverse(Quaterniond value)
    {
        var lengthSquared = value.LengthSquared();
        return new Quaterniond(-value.X / lengthSquared, -value.Y / lengthSquared, -value.Z / lengthSquared, value.W / lengthSquared);
    }

    /// <summary>
    /// The quaternion times the reciprocal of its length (<c>1 / Length()</c> once, then a multiply per component): at most
    /// 1 ulp from a true division per component. Zero gives NaN in every component (1 / 0 = +∞, 0 · ∞ = NaN; no check).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaterniond Normalize(Quaterniond value)
    {
        return value * (1.0 / value.Length());
    }

    /// <summary><c>a.X·b.X + a.Y·b.Y + a.Z·b.Z + a.W·b.W</c>, left to right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Dot(Quaterniond quaternion1, Quaterniond quaternion2)
    {
        return quaternion1.X * quaternion2.X + quaternion1.Y * quaternion2.Y + quaternion1.Z * quaternion2.Z + quaternion1.W * quaternion2.W;
    }

    /// <summary>
    /// Spherical interpolation from <paramref name="quaternion1"/> (<c>amount = 0</c>) to <paramref name="quaternion2"/>
    /// (<c>amount = 1</c>) along the shortest arc: when <c>Dot &lt; 0</c>, <paramref name="quaternion2"/> is negated (the
    /// same rotation). When the two are nearly equal (<c>Dot &gt; 1 - 1e-12</c>) it returns the normalized linear
    /// interpolation <c>normalize(a + (b - a) · amount)</c>; otherwise, with <c>θ = acos(Dot)</c>,
    /// <c>(sin((1 - amount)θ) a + sin(amount·θ) b) / sin θ</c>. Expects unit quaternions; <paramref name="amount"/> is not
    /// clamped, so values outside [0, 1] extrapolate along the same great circle. Calls acos and sin (except on the linear
    /// branch), so the result follows the OS math library and is not promised bit-identical across CPUs.
    /// </summary>
    public static Quaterniond Slerp(Quaterniond quaternion1, Quaterniond quaternion2, double amount)
    {
        var a = quaternion1;
        var b = quaternion2;
        var dot = Dot(a, b);
        if (dot < 0.0)
        {
            b = -b;
            dot = -dot;
        }

        if (dot > 1.0 - 1e-12)
            return Normalize(a + (b - a) * amount);

        var theta = Math.Acos(dot);
        var weightA = Math.Sin((1.0 - amount) * theta);
        var weightB = Math.Sin(amount * theta);
        var sinTheta = Math.Sin(theta);
        return new Quaterniond(
            (a.X * weightA + b.X * weightB) / sinTheta,
            (a.Y * weightA + b.Y * weightB) / sinTheta,
            (a.Z * weightA + b.Z * weightB) / sinTheta,
            (a.W * weightA + b.W * weightB) / sinTheta);
    }

    /// <summary>
    /// The Hamilton product: <c>(a * b)</c> rotates by <paramref name="value2"/> first, then by <paramref name="value1"/>.
    /// <c>x = aw bx + ax bw + ay bz - az by</c>, <c>y = aw by - ax bz + ay bw + az bx</c>,
    /// <c>z = aw bz + ax by - ay bx + az bw</c>, <c>w = aw bw - ax bx - ay by - az bz</c>, each left to right.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaterniond operator *(Quaterniond value1, Quaterniond value2)
    {
        var a = value1;
        var b = value2;
        return new Quaterniond(
            a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
            a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
            a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W,
            a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaterniond operator +(Quaterniond value1, Quaterniond value2)
    {
        return new Quaterniond(value1.X + value2.X, value1.Y + value2.Y, value1.Z + value2.Z, value1.W + value2.W);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaterniond operator -(Quaterniond value1, Quaterniond value2)
    {
        return new Quaterniond(value1.X - value2.X, value1.Y - value2.Y, value1.Z - value2.Z, value1.W - value2.W);
    }

    /// <summary>IEEE negation of every component (the same rotation for a unit quaternion).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaterniond operator -(Quaterniond value)
    {
        return new Quaterniond(-value.X, -value.Y, -value.Z, -value.W);
    }

    /// <summary>Every component times <paramref name="value2"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Quaterniond operator *(Quaterniond value1, double value2)
    {
        return new Quaterniond(value1.X * value2, value1.Y * value2, value1.Z * value2, value1.W * value2);
    }

    /// <summary>IEEE comparison per component: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Quaterniond left, Quaterniond right)
    {
        return left.X == right.X && left.Y == right.Y && left.Z == right.Z && left.W == right.W;
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Quaterniond left, Quaterniond right)
    {
        return !(left == right);
    }

    /// <summary>Exact: every float is a double.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Quaterniond(System.Numerics.Quaternion value)
    {
        return new Quaterniond(value.X, value.Y, value.Z, value.W);
    }

    /// <summary>Each component rounded to the nearest float.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator System.Numerics.Quaternion(Quaterniond value)
    {
        return new System.Numerics.Quaternion((float)value.X, (float)value.Y, (float)value.Z, (float)value.W);
    }

    /// <summary><see cref="double.Equals(double)"/> per component: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Quaterniond other)
    {
        return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Quaterniond other && Equals(other);
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
}
