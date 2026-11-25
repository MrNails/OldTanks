using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace CoolEngine.Mathematics;

/// <summary>
/// A float 4 × 4 matrix for column vectors (<c>M * v</c>), stored column-major: <see cref="Column0"/> …
/// <see cref="Column3"/>, 64 bytes, so the memory is <c>m00 m10 m20 m30 m01 …</c> and uploads to GLSL as is.
/// <c>A * B</c> applied to a vector applies <c>B</c> first, then <c>A</c>. Right-handed (view space looks down -Z);
/// projections map depth to [0, 1] with reversed Z (near plane 1, far plane 0); angles in radians. Built with
/// <see cref="FromColumns"/>, <see cref="FromRows"/>, the factories or <see cref="Matrix4(Matrix3)"/>; there is no
/// element constructor.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Matrix4 : IEquatable<Matrix4>
{
    public Vector4 Column0;
    public Vector4 Column1;
    public Vector4 Column2;
    public Vector4 Column3;

    public const int SizeInBytes = 64;

    /// <summary>
    /// <paramref name="topLeft"/> as the upper-left 3 × 3; the last row and column from <see cref="Identity"/>
    /// (zeros, and 1 in the corner).
    /// </summary>
    public Matrix4(Matrix3 topLeft)
    {
        Column0 = new Vector4(topLeft.Column0, 0f);
        Column1 = new Vector4(topLeft.Column1, 0f);
        Column2 = new Vector4(topLeft.Column2, 0f);
        Column3 = Vector4.UnitW;
    }

    public static Matrix4 Identity => FromColumns(Vector4.UnitX, Vector4.UnitY, Vector4.UnitZ, Vector4.UnitW);

    public static Matrix4 Zero => default;

    /// <summary>The matrix whose columns are the arguments.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4 FromColumns(Vector4 column0, Vector4 column1, Vector4 column2, Vector4 column3)
    {
        Matrix4 result;
        result.Column0 = column0;
        result.Column1 = column1;
        result.Column2 = column2;
        result.Column3 = column3;
        return result;
    }

    /// <summary>The matrix whose rows are the arguments (stored transposed, as columns).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4 FromRows(Vector4 row0, Vector4 row1, Vector4 row2, Vector4 row3)
    {
        return FromColumns(
            new Vector4(row0.X, row1.X, row2.X, row3.X),
            new Vector4(row0.Y, row1.Y, row2.Y, row3.Y),
            new Vector4(row0.Z, row1.Z, row2.Z, row3.Z),
            new Vector4(row0.W, row1.W, row2.W, row3.W));
    }

    /// <summary>
    /// The element in row <paramref name="row"/> and column <paramref name="column"/>, 0-based: component
    /// <paramref name="row"/> of <c>Column{column}</c>. Throws <see cref="ArgumentOutOfRangeException"/> outside 0 … 3.
    /// </summary>
    public float this[int row, int column]
    {
        readonly get
        {
            if ((uint)row >= 4)
                throw new ArgumentOutOfRangeException(nameof(row), row, "A Matrix4 row is 0, 1, 2 or 3.");

            return column switch
            {
                0 => Column0[row],
                1 => Column1[row],
                2 => Column2[row],
                3 => Column3[row],
                _ => throw new ArgumentOutOfRangeException(nameof(column), column, "A Matrix4 column is 0, 1, 2 or 3."),
            };
        }
        set
        {
            if ((uint)row >= 4)
                throw new ArgumentOutOfRangeException(nameof(row), row, "A Matrix4 row is 0, 1, 2 or 3.");

            switch (column)
            {
                case 0: Column0[row] = value; break;
                case 1: Column1[row] = value; break;
                case 2: Column2[row] = value; break;
                case 3: Column3[row] = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(column), column, "A Matrix4 column is 0, 1, 2 or 3.");
            }
        }
    }

    /// <summary>The translation part, <c>Column3.Xyz</c>; setting it keeps <c>Column3.W</c>.</summary>
    public Vector3 Translation
    {
        readonly get => Column3.Xyz;
        set => Column3.Xyz = value;
    }

    /// <summary>
    /// The determinant, by the same 2 × 2 sub-determinant expansion that <see cref="TryInvert"/> uses, so both agree on
    /// which matrices are singular.
    /// </summary>
    public readonly float Determinant
    {
        get
        {
            var s = new SubDeterminants(this);
            return s.Determinant;
        }
    }

    public readonly Matrix4 Transposed()
    {
        return FromRows(Column0, Column1, Column2, Column3);
    }

    /// <summary>The inverse; throws <see cref="InvalidOperationException"/> when <see cref="Determinant"/> is exactly 0.</summary>
    public readonly Matrix4 Inverted()
    {
        if (!TryInvert(this, out var result))
            throw new InvalidOperationException("The matrix is singular (its determinant is 0) and has no inverse.");

        return result;
    }

    /// <summary>
    /// The inverse, the adjugate (cofactors from 2 × 2 sub-determinants of the top two and bottom two rows) divided by
    /// the determinant. When <see cref="Determinant"/> is exactly 0, returns false and sets every element of
    /// <paramref name="result"/> to NaN.
    /// </summary>
    public static bool TryInvert(Matrix4 matrix, out Matrix4 result)
    {
        var d = new SubDeterminants(matrix);
        var determinant = d.Determinant;
        if (determinant == 0f)
        {
            var nan = new Vector4(float.NaN);
            result = FromColumns(nan, nan, nan, nan);
            return false;
        }

        var m = matrix;
        var a00 = m.Column0.X; var a10 = m.Column0.Y; var a20 = m.Column0.Z; var a30 = m.Column0.W;
        var a01 = m.Column1.X; var a11 = m.Column1.Y; var a21 = m.Column1.Z; var a31 = m.Column1.W;
        var a02 = m.Column2.X; var a12 = m.Column2.Y; var a22 = m.Column2.Z; var a32 = m.Column2.W;
        var a03 = m.Column3.X; var a13 = m.Column3.Y; var a23 = m.Column3.Z; var a33 = m.Column3.W;

        // b[r][c] is the cofactor of element (c, r) (the adjugate); the inverse is b / determinant, stored column-major.
        var b00 = a11 * d.C5 - a12 * d.C4 + a13 * d.C3;
        var b01 = -a01 * d.C5 + a02 * d.C4 - a03 * d.C3;
        var b02 = a31 * d.S5 - a32 * d.S4 + a33 * d.S3;
        var b03 = -a21 * d.S5 + a22 * d.S4 - a23 * d.S3;
        var b10 = -a10 * d.C5 + a12 * d.C2 - a13 * d.C1;
        var b11 = a00 * d.C5 - a02 * d.C2 + a03 * d.C1;
        var b12 = -a30 * d.S5 + a32 * d.S2 - a33 * d.S1;
        var b13 = a20 * d.S5 - a22 * d.S2 + a23 * d.S1;
        var b20 = a10 * d.C4 - a11 * d.C2 + a13 * d.C0;
        var b21 = -a00 * d.C4 + a01 * d.C2 - a03 * d.C0;
        var b22 = a30 * d.S4 - a31 * d.S2 + a33 * d.S0;
        var b23 = -a20 * d.S4 + a21 * d.S2 - a23 * d.S0;
        var b30 = -a10 * d.C3 + a11 * d.C1 - a12 * d.C0;
        var b31 = a00 * d.C3 - a01 * d.C1 + a02 * d.C0;
        var b32 = -a30 * d.S3 + a31 * d.S1 - a32 * d.S0;
        var b33 = a20 * d.S3 - a21 * d.S1 + a22 * d.S0;

        var inverseDeterminant = 1f / determinant;
        result = FromColumns(
            new Vector4(b00, b10, b20, b30) * inverseDeterminant,
            new Vector4(b01, b11, b21, b31) * inverseDeterminant,
            new Vector4(b02, b12, b22, b32) * inverseDeterminant,
            new Vector4(b03, b13, b23, b33) * inverseDeterminant);
        return true;
    }

    /// <summary>
    /// This matrix with the upper-left 3 × 3 replaced by identity: <c>Column0.Xyz</c>, <c>Column1.Xyz</c>,
    /// <c>Column2.Xyz</c> become the unit axes; every <c>W</c> and <see cref="Column3"/> are kept.
    /// </summary>
    public readonly Matrix4 ClearRotation()
    {
        var result = this;
        result.Column0.Xyz = Vector3.UnitX;
        result.Column1.Xyz = Vector3.UnitY;
        result.Column2.Xyz = Vector3.UnitZ;
        return result;
    }

    /// <summary>The uniform scale: <paramref name="scale"/> on the first three diagonal elements, 1 in the corner.</summary>
    public static Matrix4 CreateScale(float scale)
    {
        return CreateScale(scale, scale, scale);
    }

    /// <summary>The scale with <paramref name="scale"/>'s components on the diagonal, 1 in the corner.</summary>
    public static Matrix4 CreateScale(Vector3 scale)
    {
        return CreateScale(scale.X, scale.Y, scale.Z);
    }

    /// <summary>The scale with <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>, 1 on the diagonal.</summary>
    public static Matrix4 CreateScale(float x, float y, float z)
    {
        return FromColumns(new Vector4(x, 0f, 0f, 0f), new Vector4(0f, y, 0f, 0f), new Vector4(0f, 0f, z, 0f), Vector4.UnitW);
    }

    /// <summary><see cref="Identity"/> with <c>Column3.Xyz</c> = <paramref name="translation"/>.</summary>
    public static Matrix4 CreateTranslation(Vector3 translation)
    {
        return CreateTranslation(translation.X, translation.Y, translation.Z);
    }

    /// <summary><see cref="Identity"/> with <c>Column3.Xyz</c> = (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>).</summary>
    public static Matrix4 CreateTranslation(float x, float y, float z)
    {
        return FromColumns(Vector4.UnitX, Vector4.UnitY, Vector4.UnitZ, new Vector4(x, y, z, 1f));
    }

    /// <summary><see cref="Matrix3.CreateRotationX"/> in the upper-left 3 × 3: turns +Y toward +Z.</summary>
    public static Matrix4 CreateRotationX(float angle)
    {
        return new Matrix4(Matrix3.CreateRotationX(angle));
    }

    /// <summary><see cref="Matrix3.CreateRotationY"/> in the upper-left 3 × 3: turns +Z toward +X.</summary>
    public static Matrix4 CreateRotationY(float angle)
    {
        return new Matrix4(Matrix3.CreateRotationY(angle));
    }

    /// <summary><see cref="Matrix3.CreateRotationZ"/> in the upper-left 3 × 3: turns +X toward +Y.</summary>
    public static Matrix4 CreateRotationZ(float angle)
    {
        return new Matrix4(Matrix3.CreateRotationZ(angle));
    }

    /// <summary><see cref="Matrix3.CreateFromAxisAngle"/> in the upper-left 3 × 3; a zero axis gives <see cref="Identity"/>.</summary>
    public static Matrix4 CreateFromAxisAngle(Vector3 axis, float angle)
    {
        return new Matrix4(Matrix3.CreateFromAxisAngle(axis, angle));
    }

    /// <summary><see cref="Matrix3.CreateFromQuaternion"/> in the upper-left 3 × 3; expects a unit quaternion.</summary>
    public static Matrix4 CreateFromQuaternion(Quaternion rotation)
    {
        return new Matrix4(Matrix3.CreateFromQuaternion(rotation));
    }

    /// <summary>
    /// The right-handed view matrix of a camera at <paramref name="eye"/> looking at <paramref name="target"/>:
    /// <c>z = normalize(eye - target)</c>, <c>x = normalize(cross(up, z))</c>, <c>y = cross(z, x)</c>; rows
    /// <c>(x, -dot(x, eye)) (y, -dot(y, eye)) (z, -dot(z, eye)) (0, 0, 0, 1)</c>. View space looks down -Z. No checks:
    /// <paramref name="eye"/> equal to <paramref name="target"/>, or <paramref name="up"/> parallel to the view
    /// direction, gives NaN.
    /// </summary>
    public static Matrix4 LookAt(Vector3 eye, Vector3 target, Vector3 up)
    {
        var z = Vector3.Normalize(eye - target);
        var x = Vector3.Normalize(Vector3.Cross(up, z));
        var y = Vector3.Cross(z, x);

        return FromColumns(
            new Vector4(x.X, y.X, z.X, 0f),
            new Vector4(x.Y, y.Y, z.Y, 0f),
            new Vector4(x.Z, y.Z, z.Z, 0f),
            new Vector4(-Vector3.Dot(x, eye), -Vector3.Dot(y, eye), -Vector3.Dot(z, eye), 1f));
    }

    /// <summary>
    /// The right-handed perspective projection with reversed Z: depth (z / w) 1 on the near plane and 0 on the far
    /// plane. With <c>f = 1 / tan(fovY / 2)</c>, <c>n</c> = <paramref name="depthNear"/> and <c>F</c> =
    /// <paramref name="depthFar"/>, rows <c>(f/aspect,0,0,0) (0,f,0,0) (0,0,n/(F-n),n·F/(F-n)) (0,0,-1,0)</c>.
    /// Throws <see cref="ArgumentOutOfRangeException"/> unless <c>0 &lt; fovY &lt; π</c>, <c>aspectRatio &gt; 0</c>,
    /// <c>depthNear &gt; 0</c> and <c>depthFar &gt; depthNear</c> (NaN fails every check).
    /// </summary>
    public static Matrix4 CreatePerspectiveFieldOfView(float fovY, float aspectRatio, float depthNear, float depthFar)
    {
        CheckPerspective(fovY, aspectRatio, depthNear);
        if (!(depthFar > depthNear))
            throw new ArgumentOutOfRangeException(nameof(depthFar), depthFar, "The far plane must lie beyond the near plane.");

        var f = 1f / MathF.Tan(fovY * 0.5f);
        var range = depthFar - depthNear;

        return FromColumns(
            new Vector4(f / aspectRatio, 0f, 0f, 0f),
            new Vector4(0f, f, 0f, 0f),
            new Vector4(0f, 0f, depthNear / range, -1f),
            new Vector4(0f, 0f, depthNear * depthFar / range, 0f));
    }

    /// <summary>
    /// <see cref="CreatePerspectiveFieldOfView"/> with the far plane at infinity: the third row is <c>(0, 0, 0, n)</c>,
    /// so depth = <c>n / distance</c>, 1 on the near plane and toward 0 far away. Throws
    /// <see cref="ArgumentOutOfRangeException"/> unless <c>0 &lt; fovY &lt; π</c>, <c>aspectRatio &gt; 0</c> and
    /// <c>depthNear &gt; 0</c>.
    /// </summary>
    public static Matrix4 CreatePerspectiveFieldOfViewInfinite(float fovY, float aspectRatio, float depthNear)
    {
        CheckPerspective(fovY, aspectRatio, depthNear);

        var f = 1f / MathF.Tan(fovY * 0.5f);

        return FromColumns(
            new Vector4(f / aspectRatio, 0f, 0f, 0f),
            new Vector4(0f, f, 0f, 0f),
            new Vector4(0f, 0f, 0f, -1f),
            new Vector4(0f, 0f, depthNear, 0f));
    }

    /// <summary>
    /// The right-handed orthographic projection of the box [left, right] × [bottom, top] × [-depthFar, -depthNear]
    /// onto x, y in [-1, 1] and depth in [0, 1] with reversed Z: depth 1 at <c>z = -depthNear</c>, 0 at
    /// <c>z = -depthFar</c> (a negative <paramref name="depthNear"/> is allowed). Rows
    /// <c>(2/(r-l),0,0,-(r+l)/(r-l)) (0,2/(t-b),0,-(t+b)/(t-b)) (0,0,1/(F-n),F/(F-n)) (0,0,0,1)</c>. Throws
    /// <see cref="ArgumentException"/> when <c>left == right</c>, <c>bottom == top</c> or <c>depthNear == depthFar</c>.
    /// </summary>
    public static Matrix4 CreateOrthographicOffCenter(float left, float right, float bottom, float top, float depthNear,
        float depthFar)
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
            new Vector4(2f / width, 0f, 0f, 0f),
            new Vector4(0f, 2f / height, 0f, 0f),
            new Vector4(0f, 0f, 1f / depth, 0f),
            new Vector4(-(right + left) / width, -(top + bottom) / height, depthFar / depth, 1f));
    }

    /// <summary>The column-vector product: <c>(left * right) * v == left * (right * v)</c>, so <paramref name="right"/> applies first.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4 operator *(Matrix4 left, Matrix4 right)
    {
        Matrix4 result;
        result.Column0 = Transform(left, right.Column0);
        result.Column1 = Transform(left, right.Column1);
        result.Column2 = Transform(left, right.Column2);
        result.Column3 = Transform(left, right.Column3);
        return result;
    }

    /// <summary><c>M * v</c>: <c>Column0 * v.X + Column1 * v.Y + Column2 * v.Z + Column3 * v.W</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator *(Matrix4 matrix, Vector4 vector)
    {
        return Transform(matrix, vector);
    }

    /// <summary>IEEE comparison per element: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Matrix4 left, Matrix4 right)
    {
        return left.Column0 == right.Column0 && left.Column1 == right.Column1 && left.Column2 == right.Column2
               && left.Column3 == right.Column3;
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Matrix4 left, Matrix4 right)
    {
        return !(left == right);
    }

    /// <summary><see cref="float.Equals(float)"/> per element: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Matrix4 other)
    {
        return Column0.Equals(other.Column0) && Column1.Equals(other.Column1) && Column2.Equals(other.Column2)
               && Column3.Equals(other.Column3);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Matrix4 other && Equals(other);
    }

    /// <summary>Combines the columns' hashes, which agree with <see cref="Equals(Matrix4)"/> per element.</summary>
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector4 Transform(in Matrix4 matrix, Vector4 vector)
    {
        var result = Simd.Load(matrix.Column0) * vector.X;
        result = Vector128.MultiplyAddEstimate(Simd.Load(matrix.Column1), Vector128.Create(vector.Y), result);
        result = Vector128.MultiplyAddEstimate(Simd.Load(matrix.Column2), Vector128.Create(vector.Z), result);
        result = Vector128.MultiplyAddEstimate(Simd.Load(matrix.Column3), Vector128.Create(vector.W), result);
        return Simd.ToVector4(result);
    }

    private static void CheckPerspective(float fovY, float aspectRatio, float depthNear)
    {
        if (!(fovY > 0f && fovY < MathF.PI))
            throw new ArgumentOutOfRangeException(nameof(fovY), fovY, "The vertical field of view must lie in (0, π) radians.");
        if (!(aspectRatio > 0f))
            throw new ArgumentOutOfRangeException(nameof(aspectRatio), aspectRatio, "The aspect ratio must be positive.");
        if (!(depthNear > 0f))
            throw new ArgumentOutOfRangeException(nameof(depthNear), depthNear, "The near plane distance must be positive.");
    }

    /// <summary>
    /// The 2 × 2 sub-determinants of the top two rows (<c>S0…S5</c>) and of the bottom two rows (<c>C0…C5</c>), from
    /// which the determinant and every cofactor follow (Laplace expansion along the top two rows).
    /// </summary>
    private readonly struct SubDeterminants
    {
        public readonly float S0, S1, S2, S3, S4, S5;
        public readonly float C0, C1, C2, C3, C4, C5;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SubDeterminants(in Matrix4 m)
        {
            // aRC = row R, column C.
            var a00 = m.Column0.X; var a10 = m.Column0.Y; var a20 = m.Column0.Z; var a30 = m.Column0.W;
            var a01 = m.Column1.X; var a11 = m.Column1.Y; var a21 = m.Column1.Z; var a31 = m.Column1.W;
            var a02 = m.Column2.X; var a12 = m.Column2.Y; var a22 = m.Column2.Z; var a32 = m.Column2.W;
            var a03 = m.Column3.X; var a13 = m.Column3.Y; var a23 = m.Column3.Z; var a33 = m.Column3.W;

            S0 = a00 * a11 - a10 * a01;
            S1 = a00 * a12 - a10 * a02;
            S2 = a00 * a13 - a10 * a03;
            S3 = a01 * a12 - a11 * a02;
            S4 = a01 * a13 - a11 * a03;
            S5 = a02 * a13 - a12 * a03;

            C5 = a22 * a33 - a32 * a23;
            C4 = a21 * a33 - a31 * a23;
            C3 = a21 * a32 - a31 * a22;
            C2 = a20 * a33 - a30 * a23;
            C1 = a20 * a32 - a30 * a22;
            C0 = a20 * a31 - a30 * a21;
        }

        public float Determinant
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => S0 * C5 - S1 * C4 + S2 * C3 + S3 * C2 - S4 * C1 + S5 * C0;
        }
    }
}
