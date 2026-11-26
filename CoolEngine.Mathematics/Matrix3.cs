using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace CoolEngine.Mathematics;

/// <summary>
/// A float 3 × 3 matrix for column vectors (<c>M * v</c>), stored column-major: <see cref="Column0"/>,
/// <see cref="Column1"/>, <see cref="Column2"/>, 36 bytes. <c>A * B</c> applied to a vector applies <c>B</c> first,
/// then <c>A</c>. Right-handed; angles in radians. Built with <see cref="FromColumns"/>, <see cref="FromRows"/>, the
/// factories or <see cref="Matrix3(Matrix4)"/>; there is no element constructor.
/// Bit stability: every member that uses only + − × ÷ and sqrt gives the same bits on every x64 CPU and instruction set
/// for finite inputs (no fused multiply-add; a NaN's sign and payload are not covered); members that call sin, cos, tan or
/// acos follow the OS math library and say so.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Matrix3 : IEquatable<Matrix3>
{
    public Vector3 Column0;
    public Vector3 Column1;
    public Vector3 Column2;

    public const int SizeInBytes = 36;

    /// <summary>The upper-left 3 × 3 of <paramref name="matrix"/>.</summary>
    public Matrix3(Matrix4 matrix)
    {
        Column0 = matrix.Column0.AsVector128().AsVector3();
        Column1 = matrix.Column1.AsVector128().AsVector3();
        Column2 = matrix.Column2.AsVector128().AsVector3();
    }

    public static Matrix3 Identity => FromColumns(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);

    public static Matrix3 Zero => default;

    /// <summary>The matrix whose columns are the arguments.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3 FromColumns(Vector3 column0, Vector3 column1, Vector3 column2)
    {
        Matrix3 result;
        result.Column0 = column0;
        result.Column1 = column1;
        result.Column2 = column2;
        return result;
    }

    /// <summary>The matrix whose rows are the arguments (stored transposed, as columns).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3 FromRows(Vector3 row0, Vector3 row1, Vector3 row2)
    {
        return FromColumns(
            new Vector3(row0.X, row1.X, row2.X),
            new Vector3(row0.Y, row1.Y, row2.Y),
            new Vector3(row0.Z, row1.Z, row2.Z));
    }

    /// <summary>
    /// The element in row <paramref name="row"/> and column <paramref name="column"/>, 0-based: component
    /// <paramref name="row"/> of <c>Column{column}</c>. Throws <see cref="ArgumentOutOfRangeException"/> outside 0 … 2.
    /// </summary>
    public float this[int row, int column]
    {
        readonly get
        {
            if ((uint)row >= 3)
                throw new ArgumentOutOfRangeException(nameof(row), row, "A Matrix3 row is 0, 1 or 2.");

            return column switch
            {
                0 => Column0[row],
                1 => Column1[row],
                2 => Column2[row],
                _ => throw new ArgumentOutOfRangeException(nameof(column), column, "A Matrix3 column is 0, 1 or 2."),
            };
        }
        set
        {
            if ((uint)row >= 3)
                throw new ArgumentOutOfRangeException(nameof(row), row, "A Matrix3 row is 0, 1 or 2.");

            switch (column)
            {
                case 0: Column0[row] = value; break;
                case 1: Column1[row] = value; break;
                case 2: Column2[row] = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(column), column, "A Matrix3 column is 0, 1 or 2.");
            }
        }
    }

    /// <summary>The scalar triple product <c>Dot(Column0, Cross(Column1, Column2))</c>.</summary>
    public readonly float Determinant
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector3.Dot(Column0, Vector3.Cross(Column1, Column2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Matrix3 Transposed()
    {
        return FromRows(Column0, Column1, Column2);
    }

    /// <summary>
    /// The inverse; throws <see cref="InvalidOperationException"/> when <see cref="TryInvert"/> returns false (the
    /// determinant or its reciprocal is not finite).
    /// </summary>
    public readonly Matrix3 Inverted()
    {
        if (!TryInvert(this, out var result))
            throw new InvalidOperationException("The matrix has no finite inverse (its determinant or 1 / determinant is not finite).");

        return result;
    }

    /// <summary>
    /// The inverse: its rows are <c>Cross(Column1, Column2)</c>, <c>Cross(Column2, Column0)</c> and
    /// <c>Cross(Column0, Column1)</c> times <c>1 / det</c> (the adjugate). When the determinant or its reciprocal is not
    /// finite (a zero, tiny subnormal or overflowing determinant, or a NaN element), returns false and sets every element of
    /// <paramref name="result"/> to NaN. With both finite it returns true, and an element of a very large inverse can still
    /// overflow to infinity.
    /// </summary>
    public static bool TryInvert(Matrix3 matrix, out Matrix3 result)
    {
        var row0 = Vector3.Cross(matrix.Column1, matrix.Column2);
        var determinant = Vector3.Dot(matrix.Column0, row0);
        var inverseDeterminant = 1f / determinant;
        if (!float.IsFinite(determinant) || !float.IsFinite(inverseDeterminant))
        {
            result = FromColumns(new Vector3(float.NaN), new Vector3(float.NaN), new Vector3(float.NaN));
            return false;
        }

        result = FromRows(
            row0 * inverseDeterminant,
            Vector3.Cross(matrix.Column2, matrix.Column0) * inverseDeterminant,
            Vector3.Cross(matrix.Column0, matrix.Column1) * inverseDeterminant);
        return true;
    }

    /// <summary>The uniform scale: <paramref name="scale"/> on the diagonal.</summary>
    public static Matrix3 CreateScale(float scale)
    {
        return CreateScale(scale, scale, scale);
    }

    /// <summary>The scale with <paramref name="scale"/>'s components on the diagonal.</summary>
    public static Matrix3 CreateScale(Vector3 scale)
    {
        return CreateScale(scale.X, scale.Y, scale.Z);
    }

    /// <summary>The scale with <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/> on the diagonal.</summary>
    public static Matrix3 CreateScale(float x, float y, float z)
    {
        return FromColumns(new Vector3(x, 0f, 0f), new Vector3(0f, y, 0f), new Vector3(0f, 0f, z));
    }

    /// <summary>
    /// The rotation about +X by <paramref name="angle"/> radians: rows <c>(1,0,0) (0,c,-s) (0,s,c)</c>, turning +Y toward +Z.
    /// Calls sin and cos, so the result follows the OS math library and is not promised bit-identical across CPUs.
    /// </summary>
    public static Matrix3 CreateRotationX(float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);
        return FromColumns(new Vector3(1f, 0f, 0f), new Vector3(0f, cos, sin), new Vector3(0f, -sin, cos));
    }

    /// <summary>
    /// The rotation about +Y by <paramref name="angle"/> radians: rows <c>(c,0,s) (0,1,0) (-s,0,c)</c>, turning +Z toward +X.
    /// Calls sin and cos, so the result follows the OS math library and is not promised bit-identical across CPUs.
    /// </summary>
    public static Matrix3 CreateRotationY(float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);
        return FromColumns(new Vector3(cos, 0f, -sin), new Vector3(0f, 1f, 0f), new Vector3(sin, 0f, cos));
    }

    /// <summary>
    /// The rotation about +Z by <paramref name="angle"/> radians: rows <c>(c,-s,0) (s,c,0) (0,0,1)</c>, turning +X toward +Y.
    /// Calls sin and cos, so the result follows the OS math library and is not promised bit-identical across CPUs.
    /// </summary>
    public static Matrix3 CreateRotationZ(float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);
        return FromColumns(new Vector3(cos, sin, 0f), new Vector3(-sin, cos, 0f), new Vector3(0f, 0f, 1f));
    }

    /// <summary>
    /// The rotation by <paramref name="angle"/> radians about <paramref name="axis"/> (Rodrigues: <c>c I + (1 - c) a aᵀ + s [a]×</c>),
    /// counterclockwise when looking from the axis' tip toward the origin. The axis is normalized here, scaled by its largest
    /// component magnitude first, so every finite nonzero axis, however tiny or huge, gives the rotation about its
    /// direction; an exactly zero axis gives <see cref="Identity"/>, and a NaN or infinite component throws
    /// <see cref="ArgumentOutOfRangeException"/>, as <see cref="Matrix4.CreateFromAxisAngle"/> does. System.Numerics' own
    /// axis-angle factories (<see cref="Quaternion.CreateFromAxisAngle"/>, <c>Matrix4x4.CreateFromAxisAngle</c>) do not
    /// normalize: they expect a unit axis. Calls sin and cos, so the result follows the OS math library and is not promised
    /// bit-identical across CPUs.
    /// </summary>
    public static Matrix3 CreateFromAxisAngle(Vector3 axis, float angle)
    {
        if (!TryNormalizeAxis(axis, out var a))
            return Identity;

        var (s, c) = MathF.SinCos(angle);
        var t = 1f - c;

        var txy = t * a.X * a.Y;
        var txz = t * a.X * a.Z;
        var tyz = t * a.Y * a.Z;
        var sx = s * a.X;
        var sy = s * a.Y;
        var sz = s * a.Z;

        // Rows (c + t x², t x y - s z, t x z + s y) (t x y + s z, c + t y², t y z - s x) (t x z - s y, t y z + s x, c + t z²).
        return FromColumns(
            new Vector3(c + t * a.X * a.X, txy + sz, txz - sy),
            new Vector3(txy - sz, c + t * a.Y * a.Y, tyz + sx),
            new Vector3(txz + sy, tyz - sx, c + t * a.Z * a.Z));
    }

    /// <summary>
    /// The unit vector along <paramref name="axis"/> for the axis-angle factories (this one and
    /// <see cref="Matrix4.CreateFromAxisAngle"/>): the axis divided by its largest component magnitude (that component
    /// becomes ±1 and the squared length lies in [1, 3], so nothing overflows or underflows), then by the square root of
    /// its sum of squares, left to right, without fused multiply-add. False for an exactly zero axis; a NaN or infinite
    /// component throws <see cref="ArgumentOutOfRangeException"/>.
    /// </summary>
    internal static bool TryNormalizeAxis(Vector3 axis, out Vector3 unit)
    {
        if (!float.IsFinite(axis.X) || !float.IsFinite(axis.Y) || !float.IsFinite(axis.Z))
            throw new ArgumentOutOfRangeException(nameof(axis), axis, "The axis's components must be finite.");

        var largest = MathF.Max(MathF.Abs(axis.X), MathF.Max(MathF.Abs(axis.Y), MathF.Abs(axis.Z)));
        if (largest == 0f)
        {
            unit = default;
            return false;
        }

        var x = axis.X / largest;
        var y = axis.Y / largest;
        var z = axis.Z / largest;
        var length = MathF.Sqrt(x * x + y * y + z * z);
        unit = new Vector3(x / length, y / length, z / length);
        return true;
    }

    /// <summary>
    /// The rotation of the unit quaternion <paramref name="rotation"/> (not normalized here): rows
    /// <c>(1-2(yy+zz), 2(xy-zw), 2(xz+yw)) (2(xy+zw), 1-2(xx+zz), 2(yz-xw)) (2(xz-yw), 2(yz+xw), 1-2(xx+yy))</c>.
    /// </summary>
    public static Matrix3 CreateFromQuaternion(Quaternion rotation)
    {
        var x = rotation.X;
        var y = rotation.Y;
        var z = rotation.Z;
        var w = rotation.W;

        var xx = x * x;
        var yy = y * y;
        var zz = z * z;
        var xy = x * y;
        var xz = x * z;
        var yz = y * z;
        var xw = x * w;
        var yw = y * w;
        var zw = z * w;

        return FromColumns(
            new Vector3(1f - 2f * (yy + zz), 2f * (xy + zw), 2f * (xz - yw)),
            new Vector3(2f * (xy - zw), 1f - 2f * (xx + zz), 2f * (yz + xw)),
            new Vector3(2f * (xz + yw), 2f * (yz - xw), 1f - 2f * (xx + yy)));
    }

    /// <summary>The column-vector product: <c>(left * right) * v == left * (right * v)</c>, so <paramref name="right"/> applies first.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3 operator *(Matrix3 left, Matrix3 right)
    {
        return FromColumns(left * right.Column0, left * right.Column1, left * right.Column2);
    }

    /// <summary><c>M * v</c>: <c>Column0 * v.X + Column1 * v.Y + Column2 * v.Z</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 operator *(Matrix3 matrix, Vector3 vector)
    {
        // Multiply, then add, left to right (no fused multiply-add: the same bits on every instruction set). AsVector128
        // zeroes the unused fourth lane; AsVector3 reads back the first three.
        var result = matrix.Column0.AsVector128() * vector.X + matrix.Column1.AsVector128() * vector.Y
                     + matrix.Column2.AsVector128() * vector.Z;
        return result.AsVector3();
    }

    /// <summary>IEEE comparison per element: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Matrix3 left, Matrix3 right)
    {
        return left.Column0 == right.Column0 && left.Column1 == right.Column1 && left.Column2 == right.Column2;
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Matrix3 left, Matrix3 right)
    {
        return !(left == right);
    }

    /// <summary><see cref="float.Equals(float)"/> per element: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Matrix3 other)
    {
        return Column0.Equals(other.Column0) && Column1.Equals(other.Column1) && Column2.Equals(other.Column2);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Matrix3 other && Equals(other);
    }

    /// <summary>Combines the columns' hashes, which agree with <see cref="Equals(Matrix3)"/> per element.</summary>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Column0, Column1, Column2);
    }

    /// <summary>Invariant culture, one row per line: <c>(m00, m01, m02)\n(m10, m11, m12)\n(m20, m21, m22)</c>.</summary>
    public override readonly string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"({Column0.X}, {Column1.X}, {Column2.X})\n({Column0.Y}, {Column1.Y}, {Column2.Y})\n({Column0.Z}, {Column1.Z}, {Column2.Z})");
    }
}
