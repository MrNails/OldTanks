using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CoolEngine.Mathematics;

/// <summary>
/// The double twin of <see cref="Matrix2"/>: a 2 × 2 matrix for column vectors (<c>M * v</c>), stored column-major,
/// 32 bytes. <c>A * B</c> applied to a vector applies <c>B</c> first, then <c>A</c>. Own code, scalar, with the float
/// matrix's algorithms and operation order and no fused multiply-add. Bit stability: every member that uses only + − × ÷
/// and sqrt gives the same bits on every x64 CPU and instruction set for finite inputs (a NaN's sign and payload are not
/// covered); members that call sin, cos, tan or acos follow the OS math library and say so.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Matrix2d : IEquatable<Matrix2d>
{
    public Vector2d Column0;
    public Vector2d Column1;

    public const int SizeInBytes = 32;

    public static Matrix2d Identity => FromColumns(Vector2d.UnitX, Vector2d.UnitY);

    public static Matrix2d Zero => default;

    /// <summary>The matrix whose columns are the arguments.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2d FromColumns(Vector2d column0, Vector2d column1)
    {
        Matrix2d result;
        result.Column0 = column0;
        result.Column1 = column1;
        return result;
    }

    /// <summary>The matrix whose rows are the arguments (stored transposed, as columns).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2d FromRows(Vector2d row0, Vector2d row1)
    {
        return FromColumns(new Vector2d(row0.X, row1.X), new Vector2d(row0.Y, row1.Y));
    }

    /// <summary>
    /// The element in row <paramref name="row"/> and column <paramref name="column"/>, 0-based: component
    /// <paramref name="row"/> of <c>Column{column}</c>. Throws <see cref="ArgumentOutOfRangeException"/> outside 0 … 1.
    /// </summary>
    public double this[int row, int column]
    {
        readonly get
        {
            if ((uint)row >= 2)
                throw new ArgumentOutOfRangeException(nameof(row), row, "A Matrix2d row is 0 or 1.");

            return column switch
            {
                0 => Column0[row],
                1 => Column1[row],
                _ => throw new ArgumentOutOfRangeException(nameof(column), column, "A Matrix2d column is 0 or 1."),
            };
        }
        set
        {
            if ((uint)row >= 2)
                throw new ArgumentOutOfRangeException(nameof(row), row, "A Matrix2d row is 0 or 1.");

            switch (column)
            {
                case 0: Column0[row] = value; break;
                case 1: Column1[row] = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(column), column, "A Matrix2d column is 0 or 1.");
            }
        }
    }

    /// <summary><c>m00 m11 - m01 m10</c>.</summary>
    public readonly double Determinant
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Column0.X * Column1.Y - Column1.X * Column0.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Matrix2d Transposed()
    {
        return FromRows(Column0, Column1);
    }

    /// <summary>
    /// The inverse; throws <see cref="InvalidOperationException"/> when <see cref="TryInvert"/> returns false (the
    /// determinant or its reciprocal is not finite).
    /// </summary>
    public readonly Matrix2d Inverted()
    {
        if (!TryInvert(this, out var result))
            throw new InvalidOperationException("The matrix has no finite inverse (its determinant or 1 / determinant is not finite).");

        return result;
    }

    /// <summary>
    /// The inverse, the adjugate times <c>1 / det</c>. When the determinant or its reciprocal is not finite (a zero, tiny
    /// subnormal or overflowing determinant, or a NaN element), returns false and sets every element of
    /// <paramref name="result"/> to NaN.
    /// </summary>
    public static bool TryInvert(Matrix2d matrix, out Matrix2d result)
    {
        var determinant = matrix.Determinant;
        var inverseDeterminant = 1.0 / determinant;
        if (!double.IsFinite(determinant) || !double.IsFinite(inverseDeterminant))
        {
            result = FromColumns(new Vector2d(double.NaN), new Vector2d(double.NaN));
            return false;
        }

        // Adjugate columns (m11, -m10) and (-m01, m00). The negations are subtractions from zero, so a zero cofactor
        // stays +0: the inverse of a diagonal matrix is diagonal bit for bit for a positive determinant (a negative
        // 1 / det turns the +0 cofactors into -0).
        result = FromColumns(
            new Vector2d(matrix.Column1.Y, 0.0 - matrix.Column0.Y) * inverseDeterminant,
            new Vector2d(0.0 - matrix.Column1.X, matrix.Column0.X) * inverseDeterminant);
        return true;
    }

    /// <summary>
    /// The rotation by <paramref name="angle"/> radians: rows <c>(c, -s) (s, c)</c>, turning +X toward +Y. Calls sin and
    /// cos, so the result follows the OS math library and is not promised bit-identical across CPUs.
    /// </summary>
    public static Matrix2d CreateRotation(double angle)
    {
        var (sin, cos) = Math.SinCos(angle);
        return FromColumns(new Vector2d(cos, sin), new Vector2d(-sin, cos));
    }

    /// <summary>The uniform scale: <paramref name="scale"/> on the diagonal.</summary>
    public static Matrix2d CreateScale(double scale)
    {
        return CreateScale(scale, scale);
    }

    /// <summary>The scale with <paramref name="scale"/>'s components on the diagonal.</summary>
    public static Matrix2d CreateScale(Vector2d scale)
    {
        return CreateScale(scale.X, scale.Y);
    }

    /// <summary>The scale with <paramref name="x"/>, <paramref name="y"/> on the diagonal.</summary>
    public static Matrix2d CreateScale(double x, double y)
    {
        return FromColumns(new Vector2d(x, 0.0), new Vector2d(0.0, y));
    }

    /// <summary>The column-vector product: <c>(left * right) * v == left * (right * v)</c>, so <paramref name="right"/> applies first.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2d operator *(Matrix2d left, Matrix2d right)
    {
        return FromColumns(left * right.Column0, left * right.Column1);
    }

    /// <summary><c>M * v</c>: <c>Column0 * v.X + Column1 * v.Y</c>, multiply then add.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d operator *(Matrix2d matrix, Vector2d vector)
    {
        return new Vector2d(
            matrix.Column0.X * vector.X + matrix.Column1.X * vector.Y,
            matrix.Column0.Y * vector.X + matrix.Column1.Y * vector.Y);
    }

    /// <summary>IEEE comparison per element: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Matrix2d left, Matrix2d right)
    {
        return left.Column0 == right.Column0 && left.Column1 == right.Column1;
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Matrix2d left, Matrix2d right)
    {
        return !(left == right);
    }

    /// <summary>Exact: every float is a double.</summary>
    public static implicit operator Matrix2d(Matrix2 value)
    {
        return FromColumns(
            new Vector2d(value.Column0.X, value.Column0.Y),
            new Vector2d(value.Column1.X, value.Column1.Y));
    }

    /// <summary>Each element rounded to the nearest float.</summary>
    public static explicit operator Matrix2(Matrix2d value)
    {
        return Matrix2.FromColumns((System.Numerics.Vector2)value.Column0, (System.Numerics.Vector2)value.Column1);
    }

    /// <summary><see cref="double.Equals(double)"/> per element: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Matrix2d other)
    {
        return Column0.Equals(other.Column0) && Column1.Equals(other.Column1);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Matrix2d other && Equals(other);
    }

    /// <summary>Combines the columns' hashes, which agree with <see cref="Equals(Matrix2d)"/> per element.</summary>
    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Column0, Column1);
    }

    /// <summary>Invariant culture, one row per line: <c>(m00, m01)\n(m10, m11)</c>.</summary>
    public override readonly string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"({Column0.X}, {Column1.X})\n({Column0.Y}, {Column1.Y})");
    }
}
