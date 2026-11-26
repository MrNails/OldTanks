using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CoolEngine.Mathematics;

/// <summary>
/// The double twin of <see cref="Matrix4"/>: a 4 × 4 matrix for column vectors (<c>M * v</c>), stored column-major,
/// 128 bytes. <c>A * B</c> applied to a vector applies <c>B</c> first, then <c>A</c>. Right-handed (view space looks down
/// -Z); projections map depth to [0, 1] with reversed Z (near plane 1, far plane 0); angles in radians. Own code, scalar
/// (System.Numerics has no double matrix), with the float matrix's algorithms and operation order where the float matrix
/// has its own code and the plan's formulas where it delegates; no fused multiply-add, so the bits are the same on every
/// instruction set.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Matrix4d : IEquatable<Matrix4d>
{
    public Vector4d Column0;
    public Vector4d Column1;
    public Vector4d Column2;
    public Vector4d Column3;

    public const int SizeInBytes = 128;

    /// <summary>
    /// <paramref name="topLeft"/> as the upper-left 3 × 3; the last row and column from <see cref="Identity"/>.
    /// </summary>
    public Matrix4d(Matrix3d topLeft)
    {
        Column0 = new Vector4d(topLeft.Column0, 0.0);
        Column1 = new Vector4d(topLeft.Column1, 0.0);
        Column2 = new Vector4d(topLeft.Column2, 0.0);
        Column3 = Vector4d.UnitW;
    }

    public static Matrix4d Identity => FromColumns(Vector4d.UnitX, Vector4d.UnitY, Vector4d.UnitZ, Vector4d.UnitW);

    public static Matrix4d Zero => default;

    /// <summary>The matrix whose columns are the arguments.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4d FromColumns(Vector4d column0, Vector4d column1, Vector4d column2, Vector4d column3)
    {
        Matrix4d result;
        result.Column0 = column0;
        result.Column1 = column1;
        result.Column2 = column2;
        result.Column3 = column3;
        return result;
    }

    /// <summary>The matrix whose rows are the arguments (stored transposed, as columns).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4d FromRows(Vector4d row0, Vector4d row1, Vector4d row2, Vector4d row3)
    {
        return FromColumns(
            new Vector4d(row0.X, row1.X, row2.X, row3.X),
            new Vector4d(row0.Y, row1.Y, row2.Y, row3.Y),
            new Vector4d(row0.Z, row1.Z, row2.Z, row3.Z),
            new Vector4d(row0.W, row1.W, row2.W, row3.W));
    }

    /// <summary>
    /// The element in row <paramref name="row"/> and column <paramref name="column"/>, 0-based: component
    /// <paramref name="row"/> of <c>Column{column}</c>. Throws <see cref="ArgumentOutOfRangeException"/> outside 0 … 3.
    /// </summary>
    public double this[int row, int column]
    {
        readonly get
        {
            if ((uint)row >= 4)
                throw new ArgumentOutOfRangeException(nameof(row), row, "A Matrix4d row is 0, 1, 2 or 3.");

            return column switch
            {
                0 => Column0[row],
                1 => Column1[row],
                2 => Column2[row],
                3 => Column3[row],
                _ => throw new ArgumentOutOfRangeException(nameof(column), column, "A Matrix4d column is 0, 1, 2 or 3."),
            };
        }
        set
        {
            if ((uint)row >= 4)
                throw new ArgumentOutOfRangeException(nameof(row), row, "A Matrix4d row is 0, 1, 2 or 3.");

            switch (column)
            {
                case 0: Column0[row] = value; break;
                case 1: Column1[row] = value; break;
                case 2: Column2[row] = value; break;
                case 3: Column3[row] = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(column), column, "A Matrix4d column is 0, 1, 2 or 3.");
            }
        }
    }

    /// <summary>The translation part, the X, Y, Z of <see cref="Column3"/>; setting it keeps <c>Column3.W</c>.</summary>
    public Vector3d Translation
    {
        readonly get => new(Column3.X, Column3.Y, Column3.Z);
        set => Column3 = new Vector4d(value, Column3.W);
    }

    /// <summary>
    /// The determinant: row 0 dotted with the cofactors of row 0, the products summed as <c>(t0 + t1) + (t2 + t3)</c> (the
    /// float matrix's order). The same computation <see cref="TryInvert"/> uses, so both agree exactly on which matrices
    /// are singular (<c>Determinant == 0</c>).
    /// </summary>
    public readonly double Determinant
    {
        get
        {
            var row0 = Row(this, 0);
            var bottom = new Minors(Row(this, 2), Row(this, 3));
            return Dot(row0, Cofactors(Row(this, 1), bottom, negate: false));
        }
    }

    public readonly Matrix4d Transposed()
    {
        return FromRows(Column0, Column1, Column2, Column3);
    }

    /// <summary>The inverse; throws <see cref="InvalidOperationException"/> when <see cref="Determinant"/> is exactly 0.</summary>
    public readonly Matrix4d Inverted()
    {
        if (!TryInvert(this, out var result))
            throw new InvalidOperationException("The matrix is singular (its determinant is 0) and has no inverse.");

        return result;
    }

    /// <summary>
    /// The inverse, the adjugate times <c>1 / det</c>: the float matrix's cofactor algorithm in scalar form. Each column of
    /// the adjugate is the cofactors of one row, built from the 2 × 2 minors of the other two row pairs with the signs
    /// applied to the minors before the products, so a zero cofactor is +0 (the inverse of a diagonal matrix with a
    /// positive determinant is diagonal bit for bit). When <see cref="Determinant"/> is exactly 0, returns false and sets
    /// every element of <paramref name="result"/> to NaN.
    /// </summary>
    public static bool TryInvert(Matrix4d matrix, out Matrix4d result)
    {
        var row0 = Row(matrix, 0);
        var row1 = Row(matrix, 1);
        var row2 = Row(matrix, 2);
        var row3 = Row(matrix, 3);

        var bottom = new Minors(row2, row3);
        var top = new Minors(row0, row1);

        // Column c of the adjugate holds the cofactors of row c's elements (the adjugate is the transposed cofactor matrix).
        var column0 = Cofactors(row1, bottom, negate: false);
        var column1 = Cofactors(row0, bottom, negate: true);
        var column2 = Cofactors(row3, top, negate: false);
        var column3 = Cofactors(row2, top, negate: true);

        var determinant = Dot(row0, column0);
        if (determinant == 0.0)
        {
            var nan = new Vector4d(double.NaN);
            result = FromColumns(nan, nan, nan, nan);
            return false;
        }

        var inverseDeterminant = 1.0 / determinant;
        result = FromColumns(column0 * inverseDeterminant, column1 * inverseDeterminant, column2 * inverseDeterminant,
            column3 * inverseDeterminant);
        return true;
    }

    /// <summary>
    /// This matrix with the rotation removed and each axis's length kept (OpenTK's <c>ClearRotation</c> on identical
    /// bytes): the X, Y, Z of <see cref="Column0"/> become <c>(|Column0.xyz|, 0, 0)</c>, of <see cref="Column1"/>
    /// <c>(0, |Column1.xyz|, 0)</c>, of <see cref="Column2"/> <c>(0, 0, |Column2.xyz|)</c>; every <c>W</c> and
    /// <see cref="Column3"/> are kept. Each length is <c>sqrt(x·x + y·y + z·z)</c>.
    /// </summary>
    public readonly Matrix4d ClearRotation()
    {
        return FromColumns(
            new Vector4d(AxisLength(Column0), 0.0, 0.0, Column0.W),
            new Vector4d(0.0, AxisLength(Column1), 0.0, Column1.W),
            new Vector4d(0.0, 0.0, AxisLength(Column2), Column2.W),
            Column3);
    }

    /// <summary>
    /// The point <paramref name="position"/> transformed by this matrix: <c>this * (position, 1)</c> without its W, so the
    /// translation applies. No divide by w (use it with affine matrices, not projections).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector3d TransformPosition(Vector3d position)
    {
        var result = this * new Vector4d(position, 1.0);
        return new Vector3d(result.X, result.Y, result.Z);
    }

    /// <summary>
    /// The direction <paramref name="direction"/> transformed by this matrix: <c>this * (direction, 0)</c> without its W,
    /// so the translation does not apply.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector3d TransformDirection(Vector3d direction)
    {
        var result = this * new Vector4d(direction, 0.0);
        return new Vector3d(result.X, result.Y, result.Z);
    }

    /// <summary>The uniform scale: <paramref name="scale"/> on the first three diagonal elements, 1 in the corner.</summary>
    public static Matrix4d CreateScale(double scale)
    {
        return CreateScale(scale, scale, scale);
    }

    /// <summary>The scale with <paramref name="scale"/>'s components on the diagonal, 1 in the corner.</summary>
    public static Matrix4d CreateScale(Vector3d scale)
    {
        return CreateScale(scale.X, scale.Y, scale.Z);
    }

    /// <summary>The scale with <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>, 1 on the diagonal.</summary>
    public static Matrix4d CreateScale(double x, double y, double z)
    {
        return FromColumns(new Vector4d(x, 0.0, 0.0, 0.0), new Vector4d(0.0, y, 0.0, 0.0), new Vector4d(0.0, 0.0, z, 0.0), Vector4d.UnitW);
    }

    /// <summary><see cref="Identity"/> with the X, Y, Z of <see cref="Column3"/> = <paramref name="translation"/>.</summary>
    public static Matrix4d CreateTranslation(Vector3d translation)
    {
        return CreateTranslation(translation.X, translation.Y, translation.Z);
    }

    /// <summary><see cref="Identity"/> with the X, Y, Z of <see cref="Column3"/> = (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).</summary>
    public static Matrix4d CreateTranslation(double x, double y, double z)
    {
        return FromColumns(Vector4d.UnitX, Vector4d.UnitY, Vector4d.UnitZ, new Vector4d(x, y, z, 1.0));
    }

    /// <summary><see cref="Matrix3d.CreateRotationX"/> in the upper-left 3 × 3: turns +Y toward +Z.</summary>
    public static Matrix4d CreateRotationX(double angle)
    {
        return new Matrix4d(Matrix3d.CreateRotationX(angle));
    }

    /// <summary><see cref="Matrix3d.CreateRotationY"/> in the upper-left 3 × 3: turns +Z toward +X.</summary>
    public static Matrix4d CreateRotationY(double angle)
    {
        return new Matrix4d(Matrix3d.CreateRotationY(angle));
    }

    /// <summary><see cref="Matrix3d.CreateRotationZ"/> in the upper-left 3 × 3: turns +X toward +Y.</summary>
    public static Matrix4d CreateRotationZ(double angle)
    {
        return new Matrix4d(Matrix3d.CreateRotationZ(angle));
    }

    /// <summary><see cref="Matrix3d.CreateFromAxisAngle"/> in the upper-left 3 × 3: the axis is normalized here; a zero axis gives <see cref="Identity"/>.</summary>
    public static Matrix4d CreateFromAxisAngle(Vector3d axis, double angle)
    {
        return new Matrix4d(Matrix3d.CreateFromAxisAngle(axis, angle));
    }

    /// <summary><see cref="Matrix3d.CreateFromQuaternion"/> in the upper-left 3 × 3; expects a unit quaternion.</summary>
    public static Matrix4d CreateFromQuaternion(Quaterniond rotation)
    {
        return new Matrix4d(Matrix3d.CreateFromQuaternion(rotation));
    }

    /// <summary>
    /// The right-handed view matrix of a camera at <paramref name="eye"/> looking at <paramref name="target"/>:
    /// <c>z = normalize(eye - target)</c>, <c>x = normalize(cross(up, z))</c>, <c>y = cross(z, x)</c>; rows
    /// <c>(x, -dot(x, eye)) (y, -dot(y, eye)) (z, -dot(z, eye)) (0, 0, 0, 1)</c>. View space looks down -Z. No checks:
    /// <paramref name="eye"/> equal to <paramref name="target"/>, or <paramref name="up"/> parallel to the view direction,
    /// gives NaN.
    /// </summary>
    public static Matrix4d LookAt(Vector3d eye, Vector3d target, Vector3d up)
    {
        var z = Vector3d.Normalize(eye - target);
        var x = Vector3d.Normalize(Vector3d.Cross(up, z));
        var y = Vector3d.Cross(z, x);

        return FromColumns(
            new Vector4d(x.X, y.X, z.X, 0.0),
            new Vector4d(x.Y, y.Y, z.Y, 0.0),
            new Vector4d(x.Z, y.Z, z.Z, 0.0),
            new Vector4d(-Vector3d.Dot(x, eye), -Vector3d.Dot(y, eye), -Vector3d.Dot(z, eye), 1.0));
    }

    /// <summary>
    /// The right-handed perspective projection with reversed Z: depth (z / w) 1 on the near plane and 0 on the far plane.
    /// With <c>f = 1 / tan(fovY / 2)</c>, <c>n</c> = <paramref name="depthNear"/> and <c>F</c> = <paramref name="depthFar"/>,
    /// rows <c>(f/aspect,0,0,0) (0,f,0,0) (0,0,n/(F-n),n·F/(F-n)) (0,0,-1,0)</c>. Throws
    /// <see cref="ArgumentOutOfRangeException"/> unless <c>0 &lt; fovY &lt; π</c>, <c>aspectRatio &gt; 0</c>,
    /// <c>depthNear &gt; 0</c>, <c>depthFar &gt; depthNear</c> and <c>depthFar</c> is finite (NaN fails every check; for a
    /// far plane at infinity use <see cref="CreatePerspectiveFieldOfViewInfinite"/>). The constant <c>n·F/(F-n)</c> is
    /// computed as <c>n · (F / (F - n))</c>, so <c>n·F</c> cannot overflow.
    /// </summary>
    public static Matrix4d CreatePerspectiveFieldOfView(double fovY, double aspectRatio, double depthNear, double depthFar)
    {
        CheckPerspective(fovY, aspectRatio, depthNear);
        if (!(depthFar > depthNear))
            throw new ArgumentOutOfRangeException(nameof(depthFar), depthFar, "The far plane must lie beyond the near plane.");
        if (!double.IsFinite(depthFar))
        {
            throw new ArgumentOutOfRangeException(nameof(depthFar), depthFar,
                "The far plane must be finite; use CreatePerspectiveFieldOfViewInfinite for a far plane at infinity.");
        }

        var f = 1.0 / Math.Tan(fovY * 0.5);
        var range = depthFar - depthNear;

        return FromColumns(
            new Vector4d(f / aspectRatio, 0.0, 0.0, 0.0),
            new Vector4d(0.0, f, 0.0, 0.0),
            new Vector4d(0.0, 0.0, depthNear / range, -1.0),
            new Vector4d(0.0, 0.0, depthNear * (depthFar / range), 0.0));
    }

    /// <summary>
    /// <see cref="CreatePerspectiveFieldOfView"/> with the far plane at infinity: the third row is <c>(0, 0, 0, n)</c>, so
    /// depth = <c>n / distance</c>. Throws <see cref="ArgumentOutOfRangeException"/> unless <c>0 &lt; fovY &lt; π</c>,
    /// <c>aspectRatio &gt; 0</c> and <c>depthNear &gt; 0</c>.
    /// </summary>
    public static Matrix4d CreatePerspectiveFieldOfViewInfinite(double fovY, double aspectRatio, double depthNear)
    {
        CheckPerspective(fovY, aspectRatio, depthNear);

        var f = 1.0 / Math.Tan(fovY * 0.5);

        return FromColumns(
            new Vector4d(f / aspectRatio, 0.0, 0.0, 0.0),
            new Vector4d(0.0, f, 0.0, 0.0),
            new Vector4d(0.0, 0.0, 0.0, -1.0),
            new Vector4d(0.0, 0.0, depthNear, 0.0));
    }

    /// <summary>
    /// The right-handed orthographic projection of the box [left, right] × [bottom, top] × [-depthFar, -depthNear] onto
    /// x, y in [-1, 1] and depth in [0, 1] with reversed Z: depth 1 at <c>z = -depthNear</c>, 0 at <c>z = -depthFar</c>
    /// (a negative <paramref name="depthNear"/> is allowed). Rows
    /// <c>(2/(r-l),0,0,-(r+l)/(r-l)) (0,2/(t-b),0,-(t+b)/(t-b)) (0,0,1/(F-n),F/(F-n)) (0,0,0,1)</c>. Throws
    /// <see cref="ArgumentException"/> when <c>left == right</c>, <c>bottom == top</c> or <c>depthNear == depthFar</c>.
    /// </summary>
    public static Matrix4d CreateOrthographicOffCenter(double left, double right, double bottom, double top, double depthNear,
        double depthFar)
    {
        if (left == right)
            throw new ArgumentException("left and right must differ.", nameof(right));
        if (bottom == top)
            throw new ArgumentException("bottom and top must differ.", nameof(top));
        if (depthNear == depthFar)
            throw new ArgumentException("depthNear and depthFar must differ.", nameof(depthFar));

        var width = right - left;
        var height = top - bottom;
        var depth = depthFar - depthNear;

        return FromColumns(
            new Vector4d(2.0 / width, 0.0, 0.0, 0.0),
            new Vector4d(0.0, 2.0 / height, 0.0, 0.0),
            new Vector4d(0.0, 0.0, 1.0 / depth, 0.0),
            new Vector4d(-(right + left) / width, -(top + bottom) / height, depthFar / depth, 1.0));
    }

    /// <summary>The column-vector product: <c>(left * right) * v == left * (right * v)</c>, so <paramref name="right"/> applies first.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4d operator *(Matrix4d left, Matrix4d right)
    {
        return FromColumns(left * right.Column0, left * right.Column1, left * right.Column2, left * right.Column3);
    }

    /// <summary>
    /// <c>M * v</c>: <c>((Column0 * v.X + Column1 * v.Y) + Column2 * v.Z) + Column3 * v.W</c> per component, multiply then
    /// add, left to right (the float matrix's order).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d operator *(Matrix4d matrix, Vector4d vector)
    {
        var (x, y, z, w) = (vector.X, vector.Y, vector.Z, vector.W);
        return new Vector4d(
            matrix.Column0.X * x + matrix.Column1.X * y + matrix.Column2.X * z + matrix.Column3.X * w,
            matrix.Column0.Y * x + matrix.Column1.Y * y + matrix.Column2.Y * z + matrix.Column3.Y * w,
            matrix.Column0.Z * x + matrix.Column1.Z * y + matrix.Column2.Z * z + matrix.Column3.Z * w,
            matrix.Column0.W * x + matrix.Column1.W * y + matrix.Column2.W * z + matrix.Column3.W * w);
    }

    /// <summary>IEEE comparison per element: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Matrix4d left, Matrix4d right)
    {
        return left.Column0 == right.Column0 && left.Column1 == right.Column1 && left.Column2 == right.Column2
               && left.Column3 == right.Column3;
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Matrix4d left, Matrix4d right)
    {
        return !(left == right);
    }

    /// <summary>Exact: every float is a double.</summary>
    public static implicit operator Matrix4d(Matrix4 value)
    {
        return FromColumns(value.Column0, value.Column1, value.Column2, value.Column3);
    }

    /// <summary>Each element rounded to the nearest float.</summary>
    public static explicit operator Matrix4(Matrix4d value)
    {
        return Matrix4.FromColumns(
            (System.Numerics.Vector4)value.Column0,
            (System.Numerics.Vector4)value.Column1,
            (System.Numerics.Vector4)value.Column2,
            (System.Numerics.Vector4)value.Column3);
    }

    /// <summary><see cref="double.Equals(double)"/> per element: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Matrix4d other)
    {
        return Column0.Equals(other.Column0) && Column1.Equals(other.Column1) && Column2.Equals(other.Column2)
               && Column3.Equals(other.Column3);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Matrix4d other && Equals(other);
    }

    /// <summary>Combines the columns' hashes, which agree with <see cref="Equals(Matrix4d)"/> per element.</summary>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Column0, Column1, Column2, Column3);
    }

    /// <summary>Invariant culture, one row per line: <c>(m00, m01, m02, m03)\n(m10, …)\n…</c>.</summary>
    public override readonly string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"({Column0.X}, {Column1.X}, {Column2.X}, {Column3.X})\n({Column0.Y}, {Column1.Y}, {Column2.Y}, {Column3.Y})\n" +
            $"({Column0.Z}, {Column1.Z}, {Column2.Z}, {Column3.Z})\n({Column0.W}, {Column1.W}, {Column2.W}, {Column3.W})");
    }

    // Row r of the matrix (component r of every column).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4d Row(in Matrix4d matrix, int row)
    {
        return row switch
        {
            0 => new Vector4d(matrix.Column0.X, matrix.Column1.X, matrix.Column2.X, matrix.Column3.X),
            1 => new Vector4d(matrix.Column0.Y, matrix.Column1.Y, matrix.Column2.Y, matrix.Column3.Y),
            2 => new Vector4d(matrix.Column0.Z, matrix.Column1.Z, matrix.Column2.Z, matrix.Column3.Z),
            _ => new Vector4d(matrix.Column0.W, matrix.Column1.W, matrix.Column2.W, matrix.Column3.W),
        };
    }

    // The length of a column's X, Y, Z, left to right (no FMA).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double AxisLength(Vector4d column)
    {
        return Math.Sqrt(column.X * column.X + column.Y * column.Y + column.Z * column.Z);
    }

    // (t0 + t1) + (t2 + t3): the float matrix's fixed order.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Dot(Vector4d left, Vector4d right)
    {
        return (left.X * right.X + left.Y * right.Y) + (left.Z * right.Z + left.W * right.W);
    }

    // The cofactors of the elements of one row k (component c: the cofactor of element (k, c)): each 3 × 3 minor is
    // expanded along a second row, `elements`, using the 2 × 2 minors of the remaining two rows. Component c combines the
    // three entries of `elements` outside column c, e_X = (e1, e0, e0, e0), e_Y = (e2, e2, e1, e1), e_Z = (e3, e3, e3, e2),
    // with the minors P = (m23, m23, m13, m12), Q = (m13, m03, m03, m02), R = (m12, m02, m01, m01), signed before the
    // products as + - + - (P, R) and - + - + (Q), or all flipped when `negate`; summed left to right. This is the float
    // matrix's lane computation, one component at a time.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4d Cofactors(Vector4d elements, in Minors minors, bool negate)
    {
        var p0 = minors.M23;
        var p1 = -minors.M23;
        var p2 = minors.M13;
        var p3 = -minors.M12;
        var q0 = -minors.M13;
        var q1 = minors.M03;
        var q2 = -minors.M03;
        var q3 = minors.M02;
        var r0 = minors.M12;
        var r1 = -minors.M02;
        var r2 = minors.M01;
        var r3 = -minors.M01;

        if (negate)
        {
            p0 = -p0; p1 = -p1; p2 = -p2; p3 = -p3;
            q0 = -q0; q1 = -q1; q2 = -q2; q3 = -q3;
            r0 = -r0; r1 = -r1; r2 = -r2; r3 = -r3;
        }

        var e = elements;
        return new Vector4d(
            e.Y * p0 + e.Z * q0 + e.W * r0,
            e.X * p1 + e.Z * q1 + e.W * r1,
            e.X * p2 + e.Y * q2 + e.W * r2,
            e.X * p3 + e.Y * q3 + e.Z * r3);
    }

    private static void CheckPerspective(double fovY, double aspectRatio, double depthNear)
    {
        if (!(fovY > 0.0 && fovY < Math.PI))
            throw new ArgumentOutOfRangeException(nameof(fovY), fovY, "The vertical field of view must lie in (0, π) radians.");
        if (!(aspectRatio > 0.0))
            throw new ArgumentOutOfRangeException(nameof(aspectRatio), aspectRatio, "The aspect ratio must be positive.");
        if (!(depthNear > 0.0))
            throw new ArgumentOutOfRangeException(nameof(depthNear), depthNear, "The near plane distance must be positive.");
    }

    /// <summary>The six 2 × 2 minors of two rows <c>a</c> and <c>b</c>: <c>Mij = a_i b_j - b_i a_j</c> for column pair (i, j).</summary>
    private readonly struct Minors
    {
        public readonly double M01, M02, M03, M12, M13, M23;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Minors(Vector4d a, Vector4d b)
        {
            M01 = a.X * b.Y - b.X * a.Y;
            M02 = a.X * b.Z - b.X * a.Z;
            M03 = a.X * b.W - b.X * a.W;
            M12 = a.Y * b.Z - b.Y * a.Z;
            M13 = a.Y * b.W - b.Y * a.W;
            M23 = a.Z * b.W - b.Z * a.W;
        }
    }
}
