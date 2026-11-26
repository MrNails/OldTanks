using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CoolEngine.Mathematics;

/// <summary>
/// The double twin of <see cref="Matrix3"/>: a 3 × 3 matrix for column vectors (<c>M * v</c>), stored column-major,
/// 72 bytes. <c>A * B</c> applied to a vector applies <c>B</c> first, then <c>A</c>. Right-handed; angles in radians. Own
/// code, scalar, with the float matrix's algorithms and operation order and no fused multiply-add. Bit stability: every
/// member that uses only + − × ÷ and sqrt gives the same bits on every x64 CPU and instruction set for finite inputs (a
/// NaN's sign and payload are not covered); members that call sin, cos, tan or acos follow the OS math library and say so.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Matrix3d : IEquatable<Matrix3d>
{
    public Vector3d Column0;
    public Vector3d Column1;
    public Vector3d Column2;

    public const int SizeInBytes = 72;

    /// <summary>The upper-left 3 × 3 of <paramref name="matrix"/>.</summary>
    public Matrix3d(Matrix4d matrix)
    {
        Column0 = new Vector3d(matrix.Column0.X, matrix.Column0.Y, matrix.Column0.Z);
        Column1 = new Vector3d(matrix.Column1.X, matrix.Column1.Y, matrix.Column1.Z);
        Column2 = new Vector3d(matrix.Column2.X, matrix.Column2.Y, matrix.Column2.Z);
    }

    public static Matrix3d Identity => FromColumns(Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ);

    public static Matrix3d Zero => default;

    /// <summary>The matrix whose columns are the arguments.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3d FromColumns(Vector3d column0, Vector3d column1, Vector3d column2)
    {
        Matrix3d result;
        result.Column0 = column0;
        result.Column1 = column1;
        result.Column2 = column2;
        return result;
    }

    /// <summary>The matrix whose rows are the arguments (stored transposed, as columns).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3d FromRows(Vector3d row0, Vector3d row1, Vector3d row2)
    {
        return FromColumns(
            new Vector3d(row0.X, row1.X, row2.X),
            new Vector3d(row0.Y, row1.Y, row2.Y),
            new Vector3d(row0.Z, row1.Z, row2.Z));
    }

    /// <summary>
    /// The element in row <paramref name="row"/> and column <paramref name="column"/>, 0-based: component
    /// <paramref name="row"/> of <c>Column{column}</c>. Throws <see cref="ArgumentOutOfRangeException"/> outside 0 … 2.
    /// </summary>
    public double this[int row, int column]
    {
        readonly get
        {
            if ((uint)row >= 3)
                throw new ArgumentOutOfRangeException(nameof(row), row, "A Matrix3d row is 0, 1 or 2.");

            return column switch
            {
                0 => Column0[row],
                1 => Column1[row],
                2 => Column2[row],
                _ => throw new ArgumentOutOfRangeException(nameof(column), column, "A Matrix3d column is 0, 1 or 2."),
            };
        }
        set
        {
            if ((uint)row >= 3)
                throw new ArgumentOutOfRangeException(nameof(row), row, "A Matrix3d row is 0, 1 or 2.");

            switch (column)
            {
                case 0: Column0[row] = value; break;
                case 1: Column1[row] = value; break;
                case 2: Column2[row] = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(column), column, "A Matrix3d column is 0, 1 or 2.");
            }
        }
    }

    /// <summary>The scalar triple product <c>Dot(Column0, Cross(Column1, Column2))</c>, the same expression <see cref="TryInvert"/> tests against 0.</summary>
    public readonly double Determinant
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Vector3d.Dot(Column0, Vector3d.Cross(Column1, Column2));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Matrix3d Transposed()
    {
        return FromRows(Column0, Column1, Column2);
    }

    /// <summary>
    /// The inverse; throws <see cref="InvalidOperationException"/> when <see cref="TryInvert"/> returns false (the
    /// determinant or its reciprocal is not finite).
    /// </summary>
    public readonly Matrix3d Inverted()
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
    public static bool TryInvert(Matrix3d matrix, out Matrix3d result)
    {
        var row0 = Vector3d.Cross(matrix.Column1, matrix.Column2);
        var determinant = Vector3d.Dot(matrix.Column0, row0);
        var inverseDeterminant = 1.0 / determinant;
        if (!double.IsFinite(determinant) || !double.IsFinite(inverseDeterminant))
        {
            result = FromColumns(new Vector3d(double.NaN), new Vector3d(double.NaN), new Vector3d(double.NaN));
            return false;
        }

        result = FromRows(
            row0 * inverseDeterminant,
            Vector3d.Cross(matrix.Column2, matrix.Column0) * inverseDeterminant,
            Vector3d.Cross(matrix.Column0, matrix.Column1) * inverseDeterminant);
        return true;
    }

    /// <summary>The uniform scale: <paramref name="scale"/> on the diagonal.</summary>
    public static Matrix3d CreateScale(double scale)
    {
        return CreateScale(scale, scale, scale);
    }

    /// <summary>The scale with <paramref name="scale"/>'s components on the diagonal.</summary>
    public static Matrix3d CreateScale(Vector3d scale)
    {
        return CreateScale(scale.X, scale.Y, scale.Z);
    }

    /// <summary>The scale with <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/> on the diagonal.</summary>
    public static Matrix3d CreateScale(double x, double y, double z)
    {
        return FromColumns(new Vector3d(x, 0.0, 0.0), new Vector3d(0.0, y, 0.0), new Vector3d(0.0, 0.0, z));
    }

    /// <summary>
    /// The rotation about +X by <paramref name="angle"/> radians: rows <c>(1,0,0) (0,c,-s) (0,s,c)</c>, turning +Y toward +Z.
    /// Calls sin and cos, so the result follows the OS math library and is not promised bit-identical across CPUs.
    /// </summary>
    public static Matrix3d CreateRotationX(double angle)
    {
        var (sin, cos) = Math.SinCos(angle);
        return FromColumns(new Vector3d(1.0, 0.0, 0.0), new Vector3d(0.0, cos, sin), new Vector3d(0.0, -sin, cos));
    }

    /// <summary>
    /// The rotation about +Y by <paramref name="angle"/> radians: rows <c>(c,0,s) (0,1,0) (-s,0,c)</c>, turning +Z toward +X.
    /// Calls sin and cos, so the result follows the OS math library and is not promised bit-identical across CPUs.
    /// </summary>
    public static Matrix3d CreateRotationY(double angle)
    {
        var (sin, cos) = Math.SinCos(angle);
        return FromColumns(new Vector3d(cos, 0.0, -sin), new Vector3d(0.0, 1.0, 0.0), new Vector3d(sin, 0.0, cos));
    }

    /// <summary>
    /// The rotation about +Z by <paramref name="angle"/> radians: rows <c>(c,-s,0) (s,c,0) (0,0,1)</c>, turning +X toward +Y.
    /// Calls sin and cos, so the result follows the OS math library and is not promised bit-identical across CPUs.
    /// </summary>
    public static Matrix3d CreateRotationZ(double angle)
    {
        var (sin, cos) = Math.SinCos(angle);
        return FromColumns(new Vector3d(cos, sin, 0.0), new Vector3d(-sin, cos, 0.0), new Vector3d(0.0, 0.0, 1.0));
    }

    /// <summary>
    /// The rotation by <paramref name="angle"/> radians about <paramref name="axis"/> (Rodrigues: <c>c I + (1 - c) a aᵀ + s [a]×</c>),
    /// counterclockwise when looking from the axis' tip toward the origin. The axis is normalized here, scaled by its largest
    /// component magnitude first (as <see cref="Matrix3.CreateFromAxisAngle"/> does), so every finite nonzero axis, however
    /// tiny or huge, gives the rotation about its direction; an exactly zero axis gives <see cref="Identity"/>, and a NaN
    /// or infinite component throws <see cref="ArgumentOutOfRangeException"/>. (<see cref="Quaterniond.CreateFromAxisAngle"/>
    /// expects a unit axis instead, like System.Numerics.) Calls sin and cos, so the result follows the OS math library and
    /// is not promised bit-identical across CPUs.
    /// </summary>
    public static Matrix3d CreateFromAxisAngle(Vector3d axis, double angle)
    {
        if (!double.IsFinite(axis.X) || !double.IsFinite(axis.Y) || !double.IsFinite(axis.Z))
            throw new ArgumentOutOfRangeException(nameof(axis), axis, "The axis's components must be finite.");

        // Divided by the largest component magnitude first: that component becomes ±1 and the squared length lies in
        // [1, 3], so nothing overflows or underflows; then divided by the length, left to right, without fused multiply-add.
        var largest = Math.Max(Math.Abs(axis.X), Math.Max(Math.Abs(axis.Y), Math.Abs(axis.Z)));
        if (largest == 0.0)
            return Identity;

        var x = axis.X / largest;
        var y = axis.Y / largest;
        var z = axis.Z / largest;
        var length = Math.Sqrt(x * x + y * y + z * z);
        var a = new Vector3d(x / length, y / length, z / length);
        var (s, c) = Math.SinCos(angle);
        var t = 1.0 - c;

        var txy = t * a.X * a.Y;
        var txz = t * a.X * a.Z;
        var tyz = t * a.Y * a.Z;
        var sx = s * a.X;
        var sy = s * a.Y;
        var sz = s * a.Z;

        // Rows (c + t x², t x y - s z, t x z + s y) (t x y + s z, c + t y², t y z - s x) (t x z - s y, t y z + s x, c + t z²).
        return FromColumns(
            new Vector3d(c + t * a.X * a.X, txy + sz, txz - sy),
            new Vector3d(txy - sz, c + t * a.Y * a.Y, tyz + sx),
            new Vector3d(txz + sy, tyz - sx, c + t * a.Z * a.Z));
    }

    /// <summary>
    /// The rotation of the unit quaternion <paramref name="rotation"/> (not normalized here): rows
    /// <c>(1-2(yy+zz), 2(xy-zw), 2(xz+yw)) (2(xy+zw), 1-2(xx+zz), 2(yz-xw)) (2(xz-yw), 2(yz+xw), 1-2(xx+yy))</c>.
    /// </summary>
    public static Matrix3d CreateFromQuaternion(Quaterniond rotation)
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
            new Vector3d(1.0 - 2.0 * (yy + zz), 2.0 * (xy + zw), 2.0 * (xz - yw)),
            new Vector3d(2.0 * (xy - zw), 1.0 - 2.0 * (xx + zz), 2.0 * (yz + xw)),
            new Vector3d(2.0 * (xz + yw), 2.0 * (yz - xw), 1.0 - 2.0 * (xx + yy)));
    }

    /// <summary>The column-vector product: <c>(left * right) * v == left * (right * v)</c>, so <paramref name="right"/> applies first.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3d operator *(Matrix3d left, Matrix3d right)
    {
        return FromColumns(left * right.Column0, left * right.Column1, left * right.Column2);
    }

    /// <summary><c>M * v</c>: <c>(Column0 * v.X + Column1 * v.Y) + Column2 * v.Z</c>, multiply then add, left to right.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d operator *(Matrix3d matrix, Vector3d vector)
    {
        return new Vector3d(
            matrix.Column0.X * vector.X + matrix.Column1.X * vector.Y + matrix.Column2.X * vector.Z,
            matrix.Column0.Y * vector.X + matrix.Column1.Y * vector.Y + matrix.Column2.Y * vector.Z,
            matrix.Column0.Z * vector.X + matrix.Column1.Z * vector.Y + matrix.Column2.Z * vector.Z);
    }

    /// <summary>IEEE comparison per element: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Matrix3d left, Matrix3d right)
    {
        return left.Column0 == right.Column0 && left.Column1 == right.Column1 && left.Column2 == right.Column2;
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Matrix3d left, Matrix3d right)
    {
        return !(left == right);
    }

    /// <summary>Exact: every float is a double.</summary>
    public static implicit operator Matrix3d(Matrix3 value)
    {
        return FromColumns(value.Column0, value.Column1, value.Column2);
    }

    /// <summary>Each element rounded to the nearest float.</summary>
    public static explicit operator Matrix3(Matrix3d value)
    {
        return Matrix3.FromColumns(
            (System.Numerics.Vector3)value.Column0,
            (System.Numerics.Vector3)value.Column1,
            (System.Numerics.Vector3)value.Column2);
    }

    /// <summary><see cref="double.Equals(double)"/> per element: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Matrix3d other)
    {
        return Column0.Equals(other.Column0) && Column1.Equals(other.Column1) && Column2.Equals(other.Column2);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Matrix3d other && Equals(other);
    }

    /// <summary>Combines the columns' hashes, which agree with <see cref="Equals(Matrix3d)"/> per element.</summary>
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
