using System.Globalization;
using System.Numerics;
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
/// element constructor. Bit stability: every member that uses only + − × ÷ and sqrt gives the same bits on every x64 CPU and
/// instruction set for finite inputs (no fused multiply-add; a NaN's sign and payload are not covered); members that call
/// sin, cos, tan or acos follow the OS math library and say so.
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

    /// <summary>The translation part, the X, Y, Z of <see cref="Column3"/>; setting it keeps <c>Column3.W</c>.</summary>
    public Vector3 Translation
    {
        readonly get => Xyz(Column3);
        set => Column3 = new Vector4(value, Column3.W);
    }

    /// <summary>
    /// The determinant: row 0 dotted with the cofactors of row 0, the products summed as <c>(t0 + t1) + (t2 + t3)</c>. The
    /// same own computation (no fused multiply-add, the same order on every instruction set) that <see cref="TryInvert"/>
    /// uses, so <see cref="TryInvert"/> refuses exactly the matrices whose <see cref="Determinant"/> or its reciprocal is
    /// not finite.
    /// </summary>
    public readonly float Determinant
    {
        get
        {
            var rows = Rows(this);
            var bottom = new Minors(rows.Column2.AsVector128(), rows.Column3.AsVector128());
            var cofactors0 = Cofactors(rows.Column1.AsVector128(), bottom, SignsPlusMinus, SignsMinusPlus);
            return Dot(rows.Column0.AsVector128(), cofactors0).ToScalar();
        }
    }

    /// <summary>The transpose. Delegates to <c>System.Numerics.Matrix4x4.Transpose</c> on the same bytes.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Matrix4 Transposed()
    {
        return FromNumerics(Matrix4x4.Transpose(ToNumerics(this)));
    }

    /// <summary>
    /// The inverse; throws <see cref="InvalidOperationException"/> when <see cref="TryInvert"/> returns false (the
    /// determinant or its reciprocal is not finite).
    /// </summary>
    public readonly Matrix4 Inverted()
    {
        if (!TryInvert(this, out var result))
            throw new InvalidOperationException("The matrix has no finite inverse (its determinant or 1 / determinant is not finite).");

        return result;
    }

    /// <summary>
    /// The inverse, the adjugate times <c>1 / det</c>. Own code on <see cref="Vector128{T}"/> without fused multiply-add and
    /// with every sum in a fixed order, so the bits are the same on every instruction set (System.Numerics'
    /// <c>Matrix4x4.Invert</c> differs between FMA, SSE and software). Each column of the adjugate is the cofactors of one
    /// row, built from the 2 × 2 minors of the other two row pairs. A diagonal matrix with positive diagonal entries
    /// inverts to a diagonal one bit for bit. When the determinant or its reciprocal is not finite (a zero, tiny subnormal
    /// or overflowing determinant, or a NaN element), returns false and sets every element of <paramref name="result"/> to
    /// NaN. With both finite it returns true, and an element of a very large inverse can still overflow to infinity.
    /// </summary>
    public static bool TryInvert(Matrix4 matrix, out Matrix4 result)
    {
        var rows = Rows(matrix);
        var row0 = rows.Column0.AsVector128();
        var row1 = rows.Column1.AsVector128();
        var row2 = rows.Column2.AsVector128();
        var row3 = rows.Column3.AsVector128();

        var bottom = new Minors(row2, row3);
        var top = new Minors(row0, row1);

        // Column c of the adjugate holds the cofactors of row c's elements (the adjugate is the transposed cofactor matrix).
        var column0 = Cofactors(row1, bottom, SignsPlusMinus, SignsMinusPlus);
        var column1 = Cofactors(row0, bottom, SignsMinusPlus, SignsPlusMinus);
        var column2 = Cofactors(row3, top, SignsPlusMinus, SignsMinusPlus);
        var column3 = Cofactors(row2, top, SignsMinusPlus, SignsPlusMinus);

        // Every lane holds the determinant (see Dot), so every lane of the reciprocal holds 1 / det.
        var determinant = Dot(row0, column0);
        var inverseDeterminant = Vector128<float>.One / determinant;
        if (!float.IsFinite(determinant.ToScalar()) || !float.IsFinite(inverseDeterminant.ToScalar()))
        {
            var nan = new Vector4(float.NaN);
            result = FromColumns(nan, nan, nan, nan);
            return false;
        }

        result.Column0 = (column0 * inverseDeterminant).AsVector4();
        result.Column1 = (column1 * inverseDeterminant).AsVector4();
        result.Column2 = (column2 * inverseDeterminant).AsVector4();
        result.Column3 = (column3 * inverseDeterminant).AsVector4();
        return true;
    }

    /// <summary>
    /// This matrix with the rotation removed and each axis's length kept (OpenTK's <c>ClearRotation</c> on identical
    /// bytes): the X, Y, Z of <see cref="Column0"/> become <c>(|Column0.xyz|, 0, 0)</c>, of <see cref="Column1"/>
    /// <c>(0, |Column1.xyz|, 0)</c>, of <see cref="Column2"/> <c>(0, 0, |Column2.xyz|)</c>; every <c>W</c> and
    /// <see cref="Column3"/> are kept. For a TRS matrix the result is the translation times the scale. Each length is
    /// <c>sqrt(x·x + y·y + z·z)</c>, multiply then add, no fused multiply-add.
    /// </summary>
    public readonly Matrix4 ClearRotation()
    {
        return FromColumns(
            new Vector4(AxisLength(Column0), 0f, 0f, Column0.W),
            new Vector4(0f, AxisLength(Column1), 0f, Column1.W),
            new Vector4(0f, 0f, AxisLength(Column2), Column2.W),
            Column3);
    }

    // The length of a column's X, Y, Z, in scalar arithmetic left to right (no FMA), the same on every path.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float AxisLength(Vector4 column)
    {
        return MathF.Sqrt(column.X * column.X + column.Y * column.Y + column.Z * column.Z);
    }

    /// <summary>
    /// The point <paramref name="position"/> transformed by this matrix: <c>this * (position, 1)</c> without its W, so the
    /// translation applies. No divide by w (use it with affine matrices, not projections). Own code, no fused multiply-add:
    /// <c>((Column0 · x + Column1 · y) + Column2 · z) + Column3</c>, the same bits as <c>Matrix4 * Vector4</c> on
    /// <c>(position, 1)</c> without its multiply by 1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector3 TransformPosition(Vector3 position)
    {
        var x = Vector128.Create(position.X);
        var y = Vector128.Create(position.Y);
        var z = Vector128.Create(position.Z);
        var result = Column0.AsVector128() * x + Column1.AsVector128() * y + Column2.AsVector128() * z + Column3.AsVector128();
        return result.AsVector3();
    }

    /// <summary>
    /// The direction <paramref name="direction"/> transformed by this matrix: <c>this * (direction, 0)</c> without its W,
    /// so the translation does not apply. The same own code as <c>Matrix4 * Vector4</c> on <c>(direction, 0)</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Vector3 TransformDirection(Vector3 direction)
    {
        return Transform(this, new Vector4(direction, 0f).AsVector128()).AsVector3();
    }

    /// <summary>
    /// The uniform scale: <paramref name="scale"/> on the first three diagonal elements, 1 in the corner. Delegates to
    /// <c>System.Numerics.Matrix4x4.CreateScale</c> (a diagonal matrix has the same bytes in both conventions).
    /// </summary>
    public static Matrix4 CreateScale(float scale)
    {
        return FromNumerics(Matrix4x4.CreateScale(scale));
    }

    /// <summary>The scale with <paramref name="scale"/>'s components on the diagonal, 1 in the corner. Delegates to <c>Matrix4x4.CreateScale</c>.</summary>
    public static Matrix4 CreateScale(Vector3 scale)
    {
        return FromNumerics(Matrix4x4.CreateScale(scale));
    }

    /// <summary>The scale with <paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>, 1 on the diagonal. Delegates to <c>Matrix4x4.CreateScale</c>.</summary>
    public static Matrix4 CreateScale(float x, float y, float z)
    {
        return FromNumerics(Matrix4x4.CreateScale(x, y, z));
    }

    /// <summary>
    /// <see cref="Identity"/> with the X, Y, Z of <see cref="Column3"/> = <paramref name="translation"/>. Delegates to
    /// <c>System.Numerics.Matrix4x4.CreateTranslation</c>, whose fourth row has the bytes of our fourth column.
    /// </summary>
    public static Matrix4 CreateTranslation(Vector3 translation)
    {
        return FromNumerics(Matrix4x4.CreateTranslation(translation));
    }

    /// <summary>
    /// <see cref="Identity"/> with the X, Y, Z of <see cref="Column3"/> = (<paramref name="x"/>, <paramref name="y"/>,
    /// <paramref name="z"/>). Delegates to <c>Matrix4x4.CreateTranslation</c>.
    /// </summary>
    public static Matrix4 CreateTranslation(float x, float y, float z)
    {
        return FromNumerics(Matrix4x4.CreateTranslation(x, y, z));
    }

    /// <summary>
    /// The rotation about +X by <paramref name="angle"/> radians, rows <c>(1,0,0,0) (0,c,-s,0) (0,s,c,0) (0,0,0,1)</c>,
    /// turning +Y toward +Z. Delegates to <c>System.Numerics.Matrix4x4.CreateRotationX</c>: their row-vector matrix of
    /// the same rotation has the same bytes. Calls sin and cos, so the result follows the OS math library and is not
    /// promised bit-identical across CPUs.
    /// </summary>
    public static Matrix4 CreateRotationX(float angle)
    {
        return FromNumerics(Matrix4x4.CreateRotationX(angle));
    }

    /// <summary>
    /// The rotation about +Y, rows <c>(c,0,s,0) (0,1,0,0) (-s,0,c,0) (0,0,0,1)</c>, turning +Z toward +X. Delegates to
    /// <c>Matrix4x4.CreateRotationY</c> on the same bytes. Calls sin and cos, so the result follows the OS math library and
    /// is not promised bit-identical across CPUs.
    /// </summary>
    public static Matrix4 CreateRotationY(float angle)
    {
        return FromNumerics(Matrix4x4.CreateRotationY(angle));
    }

    /// <summary>
    /// The rotation about +Z, rows <c>(c,-s,0,0) (s,c,0,0) (0,0,1,0) (0,0,0,1)</c>, turning +X toward +Y. Delegates to
    /// <c>Matrix4x4.CreateRotationZ</c> on the same bytes. Calls sin and cos, so the result follows the OS math library and
    /// is not promised bit-identical across CPUs.
    /// </summary>
    public static Matrix4 CreateRotationZ(float angle)
    {
        return FromNumerics(Matrix4x4.CreateRotationZ(angle));
    }

    /// <summary>
    /// The rotation by <paramref name="angle"/> radians about <paramref name="axis"/>, counterclockwise when looking from
    /// the axis' tip toward the origin. The axis is normalized here, scaled by its largest component magnitude first, as
    /// <see cref="Matrix3.CreateFromAxisAngle"/> does: every finite nonzero axis, however tiny or huge, gives the rotation
    /// about its direction; an exactly zero axis gives <see cref="Identity"/>, and a NaN or infinite component throws
    /// <see cref="ArgumentOutOfRangeException"/>. The unit axis then goes to
    /// <c>System.Numerics.Matrix4x4.CreateFromAxisAngle</c>, which itself expects a unit axis. Calls sin and cos, so the
    /// result follows the OS math library and is not promised bit-identical across CPUs.
    /// </summary>
    public static Matrix4 CreateFromAxisAngle(Vector3 axis, float angle)
    {
        if (!Matrix3.TryNormalizeAxis(axis, out var unit))
            return Identity;

        return FromNumerics(Matrix4x4.CreateFromAxisAngle(unit, angle));
    }

    /// <summary>
    /// The rotation of the unit quaternion <paramref name="rotation"/> (not normalized here); the upper-left 3 × 3 is
    /// <see cref="Matrix3.CreateFromQuaternion"/>'s. Delegates to <c>System.Numerics.Matrix4x4.CreateFromQuaternion</c>
    /// on the same bytes.
    /// </summary>
    public static Matrix4 CreateFromQuaternion(Quaternion rotation)
    {
        return FromNumerics(Matrix4x4.CreateFromQuaternion(rotation));
    }

    /// <summary>
    /// The right-handed view matrix of a camera at <paramref name="eye"/> looking at <paramref name="target"/>:
    /// <c>z = normalize(eye - target)</c>, <c>x = normalize(cross(up, z))</c>, <c>y = cross(z, x)</c>; rows
    /// <c>(x, -dot(x, eye)) (y, -dot(y, eye)) (z, -dot(z, eye)) (0, 0, 0, 1)</c>. View space looks down -Z. Delegates to
    /// <c>System.Numerics.Matrix4x4.CreateLookAt</c> (right-handed) on the same bytes. No checks: <paramref name="eye"/>
    /// equal to <paramref name="target"/>, or <paramref name="up"/> parallel to the view direction, gives NaN.
    /// </summary>
    public static Matrix4 LookAt(Vector3 eye, Vector3 target, Vector3 up)
    {
        return FromNumerics(Matrix4x4.CreateLookAt(eye, target, up));
    }

    /// <summary>
    /// The right-handed perspective projection with reversed Z: depth (z / w) 1 on the near plane and 0 on the far
    /// plane. With <c>f = 1 / tan(fovY / 2)</c>, <c>n</c> = <paramref name="depthNear"/> and <c>F</c> =
    /// <paramref name="depthFar"/>, rows <c>(f/aspect,0,0,0) (0,f,0,0) (0,0,n/(F-n),n·F/(F-n)) (0,0,-1,0)</c>.
    /// Throws <see cref="ArgumentOutOfRangeException"/> unless <c>0 &lt; fovY &lt; π</c>, <c>aspectRatio &gt; 0</c>,
    /// <c>depthNear &gt; 0</c>, <c>depthFar &gt; depthNear</c> and every argument is finite (NaN fails every check; for a
    /// far plane at infinity use <see cref="CreatePerspectiveFieldOfViewInfinite"/>). The constant <c>n·F/(F-n)</c> is
    /// computed as <c>n · (F / (F - n))</c>, so <c>n·F</c> cannot overflow. Calls tan, so the result follows the OS math
    /// library and is not promised bit-identical across CPUs.
    /// </summary>
    public static Matrix4 CreatePerspectiveFieldOfView(float fovY, float aspectRatio, float depthNear, float depthFar)
    {
        CheckPerspective(fovY, aspectRatio, depthNear);
        if (!(depthFar > depthNear))
            throw new ArgumentOutOfRangeException(nameof(depthFar), depthFar, "The far plane must lie beyond the near plane.");
        if (!float.IsFinite(depthFar))
        {
            throw new ArgumentOutOfRangeException(nameof(depthFar), depthFar,
                "The far plane must be finite; use CreatePerspectiveFieldOfViewInfinite for a far plane at infinity.");
        }

        var f = 1f / MathF.Tan(fovY * 0.5f);
        var range = depthFar - depthNear;

        return FromColumns(
            new Vector4(f / aspectRatio, 0f, 0f, 0f),
            new Vector4(0f, f, 0f, 0f),
            new Vector4(0f, 0f, depthNear / range, -1f),
            new Vector4(0f, 0f, depthNear * (depthFar / range), 0f));
    }

    /// <summary>
    /// <see cref="CreatePerspectiveFieldOfView"/> with the far plane at infinity: the third row is <c>(0, 0, 0, n)</c>,
    /// so depth = <c>n / distance</c>, 1 on the near plane and toward 0 far away. Throws
    /// <see cref="ArgumentOutOfRangeException"/> unless <c>0 &lt; fovY &lt; π</c>, <c>aspectRatio &gt; 0</c>,
    /// <c>depthNear &gt; 0</c> and every argument is finite. Calls tan, so the result follows the OS math library and is
    /// not promised bit-identical across CPUs.
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
    /// <see cref="ArgumentException"/> when an argument is not finite, or when <c>left == right</c>, <c>bottom == top</c> or
    /// <c>depthNear == depthFar</c>.
    /// </summary>
    public static Matrix4 CreateOrthographicOffCenter(float left, float right, float bottom, float top, float depthNear,
        float depthFar)
    {
        CheckFinite(left, nameof(left));
        CheckFinite(right, nameof(right));
        CheckFinite(bottom, nameof(bottom));
        CheckFinite(top, nameof(top));
        CheckFinite(depthNear, nameof(depthNear));
        CheckFinite(depthFar, nameof(depthFar));
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

    /// <summary>
    /// The column-vector product: <c>(left * right) * v == left * (right * v)</c>, so <paramref name="right"/> applies
    /// first. Column j of the result is <c>left * right.Column{j}</c>. Own code on <see cref="Vector128{T}"/> without
    /// fused multiply-add, so the bits are the same on every instruction set (System.Numerics' <c>Matrix4x4.Multiply</c>
    /// fuses on FMA hardware).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4 operator *(Matrix4 left, Matrix4 right)
    {
        Matrix4 result;
        result.Column0 = Transform(left, right.Column0.AsVector128()).AsVector4();
        result.Column1 = Transform(left, right.Column1.AsVector128()).AsVector4();
        result.Column2 = Transform(left, right.Column2.AsVector128()).AsVector4();
        result.Column3 = Transform(left, right.Column3.AsVector128()).AsVector4();
        return result;
    }

    /// <summary>
    /// <c>M * v</c>: <c>((Column0 * v.X + Column1 * v.Y) + Column2 * v.Z) + Column3 * v.W</c>, multiply then add, left to
    /// right. Own code on <see cref="Vector128{T}"/> without fused multiply-add: the same bits on every instruction set.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 operator *(Matrix4 matrix, Vector4 vector)
    {
        return Transform(matrix, vector.AsVector128()).AsVector4();
    }

    /// <summary>
    /// <paramref name="applyFirst"/>, then <paramref name="applyThen"/>: <c>applyThen * applyFirst</c>, the same bits as
    /// the operator. Temporary, for OT-5's port check (OpenTK's row-vector <c>A * B</c> is <c>Compose(A, B)</c>); removed
    /// when the move is done.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4 Compose(Matrix4 applyFirst, Matrix4 applyThen)
    {
        return applyThen * applyFirst;
    }

    /// <summary>
    /// <paramref name="matrix"/> applied to <paramref name="vector"/>: <c>matrix * vector</c>, the same bits as the
    /// operator. Temporary, for OT-5's port check (OpenTK's row-vector <c>A * B</c> is <c>Compose(A, B)</c>); removed when
    /// the move is done.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Apply(Matrix4 matrix, Vector4 vector)
    {
        return matrix * vector;
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

    // M * v for a vector already in a register: ((c0 * x + c1 * y) + c2 * z) + c3 * w, multiply then add, left to right.
    // Every operation is lane-wise (the broadcasts are moves), so the hardware and software paths give the same bits.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> Transform(in Matrix4 matrix, Vector128<float> vector)
    {
        var x = Vector128.Shuffle(vector, Vector128.Create(0, 0, 0, 0));
        var y = Vector128.Shuffle(vector, Vector128.Create(1, 1, 1, 1));
        var z = Vector128.Shuffle(vector, Vector128.Create(2, 2, 2, 2));
        var w = Vector128.Shuffle(vector, Vector128.Create(3, 3, 3, 3));

        return matrix.Column0.AsVector128() * x + matrix.Column1.AsVector128() * y + matrix.Column2.AsVector128() * z
               + matrix.Column3.AsVector128() * w;
    }

    // The rows of the matrix as the columns of its transpose (the transpose only moves data, so it is the same on every path).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Matrix4 Rows(in Matrix4 matrix)
    {
        return FromNumerics(Matrix4x4.Transpose(ToNumerics(matrix)));
    }

    // Lane signs for the cofactor pattern: + - + - and - + - +, applied by flipping the sign bit (exact).
    private static Vector128<float> SignsPlusMinus => Vector128.Create(0f, float.NegativeZero, 0f, float.NegativeZero);

    private static Vector128<float> SignsMinusPlus => Vector128.Create(float.NegativeZero, 0f, float.NegativeZero, 0f);

    // The cofactors of the elements of one row k (lane c: the cofactor of element (k, c)): each 3 × 3 minor is expanded
    // along a second row, `elements`, using the 2 × 2 minors of the remaining two rows. Lane c combines the three entries
    // of `elements` outside column c:
    //   e_X = (e1, e0, e0, e0), e_Y = (e2, e2, e1, e1), e_Z = (e3, e3, e3, e2)
    // with the minors P, Q, R of the matching column pairs, each sign-flipped before the product, then summed left to right.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> Cofactors(Vector128<float> elements, in Minors minors, Vector128<float> signsOfPAndR,
        Vector128<float> signsOfQ)
    {
        var x = Vector128.Shuffle(elements, Vector128.Create(1, 0, 0, 0));
        var y = Vector128.Shuffle(elements, Vector128.Create(2, 2, 1, 1));
        var z = Vector128.Shuffle(elements, Vector128.Create(3, 3, 3, 2));

        return x * (minors.P ^ signsOfPAndR) + y * (minors.Q ^ signsOfQ) + z * (minors.R ^ signsOfPAndR);
    }

    // The dot product with the additions in a fixed order, (t0 + t1) + (t2 + t3), written as lane-wise adds of shuffled
    // copies (a library horizontal sum could add in another order on another instruction set). Every lane ends with the
    // same bits: each adds the same two pair sums, and IEEE addition is commutative.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<float> Dot(Vector128<float> left, Vector128<float> right)
    {
        var products = left * right;
        var pairs = products + Vector128.Shuffle(products, Vector128.Create(1, 0, 3, 2));
        return pairs + Vector128.Shuffle(pairs, Vector128.Create(2, 3, 0, 1));
    }

    /// <summary>
    /// The 2 × 2 minors of two rows <c>a</c> and <c>b</c> that the cofactors of the other two rows need, lane c matching
    /// <see cref="Cofactors"/>' element lanes: <c>P</c> pairs columns (2,3) (2,3) (1,3) (1,2), <c>Q</c> pairs (1,3) (0,3)
    /// (0,3) (0,2), <c>R</c> pairs (1,2) (0,2) (0,1) (0,1); the minor of columns (i, j) is <c>a_i b_j - b_i a_j</c>.
    /// </summary>
    private readonly struct Minors
    {
        public readonly Vector128<float> P;
        public readonly Vector128<float> Q;
        public readonly Vector128<float> R;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Minors(Vector128<float> a, Vector128<float> b)
        {
            var aX = Vector128.Shuffle(a, Vector128.Create(1, 0, 0, 0));
            var aY = Vector128.Shuffle(a, Vector128.Create(2, 2, 1, 1));
            var aZ = Vector128.Shuffle(a, Vector128.Create(3, 3, 3, 2));
            var bX = Vector128.Shuffle(b, Vector128.Create(1, 0, 0, 0));
            var bY = Vector128.Shuffle(b, Vector128.Create(2, 2, 1, 1));
            var bZ = Vector128.Shuffle(b, Vector128.Create(3, 3, 3, 2));

            P = aY * bZ - bY * aZ;
            Q = aX * bZ - bX * aZ;
            R = aX * bY - bX * aY;
        }
    }

    // A column-major matrix has the bytes of the row-major (System.Numerics) matrix of the same transform. Matrix4x4 stays
    // inside the implementation: it never appears in a public signature (PublicApiTests guards that).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Matrix4x4 ToNumerics(Matrix4 matrix)
    {
        return Unsafe.BitCast<Matrix4, Matrix4x4>(matrix);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Matrix4 FromNumerics(Matrix4x4 matrix)
    {
        return Unsafe.BitCast<Matrix4x4, Matrix4>(matrix);
    }

    // The first three components of a Vector4 (System.Numerics has no Xyz).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3 Xyz(Vector4 vector)
    {
        return vector.AsVector128().AsVector3();
    }

    private static void CheckPerspective(float fovY, float aspectRatio, float depthNear)
    {
        if (!(fovY > 0f && fovY < MathF.PI))
            throw new ArgumentOutOfRangeException(nameof(fovY), fovY, "The vertical field of view must lie in (0, π) radians.");
        if (!(aspectRatio > 0f && float.IsFinite(aspectRatio)))
            throw new ArgumentOutOfRangeException(nameof(aspectRatio), aspectRatio, "The aspect ratio must be positive and finite.");
        if (!(depthNear > 0f && float.IsFinite(depthNear)))
            throw new ArgumentOutOfRangeException(nameof(depthNear), depthNear, "The near plane distance must be positive and finite.");
    }

    private static void CheckFinite(float value, string name)
    {
        if (!float.IsFinite(value))
            throw new ArgumentException($"{name} must be finite; it is {value}.", name);
    }
}
