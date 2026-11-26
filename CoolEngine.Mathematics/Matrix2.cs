using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace CoolEngine.Mathematics;

/// <summary>
/// A float 2 × 2 matrix for column vectors (<c>M * v</c>), stored column-major: <see cref="Column0"/> then
/// <see cref="Column1"/>, 16 bytes. <c>A * B</c> applied to a vector applies <c>B</c> first, then <c>A</c>.
/// Built with <see cref="FromColumns"/>, <see cref="FromRows"/> or the factories; there is no element constructor.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct Matrix2 : IEquatable<Matrix2>
{
    public Vector2 Column0;
    public Vector2 Column1;

    public const int SizeInBytes = 16;

    public static Matrix2 Identity => FromColumns(Vector2.UnitX, Vector2.UnitY);

    public static Matrix2 Zero => default;

    /// <summary>The matrix whose columns are the arguments.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2 FromColumns(Vector2 column0, Vector2 column1)
    {
        Matrix2 result;
        result.Column0 = column0;
        result.Column1 = column1;
        return result;
    }

    /// <summary>The matrix whose rows are the arguments (stored transposed, as columns).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2 FromRows(Vector2 row0, Vector2 row1)
    {
        return FromColumns(new Vector2(row0.X, row1.X), new Vector2(row0.Y, row1.Y));
    }

    /// <summary>
    /// The element in row <paramref name="row"/> and column <paramref name="column"/>, 0-based: component
    /// <paramref name="row"/> of <c>Column{column}</c>. Throws <see cref="ArgumentOutOfRangeException"/> outside 0 … 1.
    /// </summary>
    public float this[int row, int column]
    {
        readonly get
        {
            if ((uint)row >= 2)
                throw new ArgumentOutOfRangeException(nameof(row), row, "A Matrix2 row is 0 or 1.");

            return column switch
            {
                0 => Column0[row],
                1 => Column1[row],
                _ => throw new ArgumentOutOfRangeException(nameof(column), column, "A Matrix2 column is 0 or 1."),
            };
        }
        set
        {
            if ((uint)row >= 2)
                throw new ArgumentOutOfRangeException(nameof(row), row, "A Matrix2 row is 0 or 1.");

            switch (column)
            {
                case 0: Column0[row] = value; break;
                case 1: Column1[row] = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(column), column, "A Matrix2 column is 0 or 1.");
            }
        }
    }

    /// <summary><c>m00 m11 - m01 m10</c>.</summary>
    public readonly float Determinant
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => Column0.X * Column1.Y - Column1.X * Column0.Y;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Matrix2 Transposed()
    {
        return FromRows(Column0, Column1);
    }

    /// <summary>The inverse; throws <see cref="InvalidOperationException"/> when <see cref="Determinant"/> is exactly 0.</summary>
    public readonly Matrix2 Inverted()
    {
        if (!TryInvert(this, out var result))
            throw new InvalidOperationException("The matrix is singular (its determinant is 0) and has no inverse.");

        return result;
    }

    /// <summary>
    /// The inverse, the adjugate divided by the determinant. When <see cref="Determinant"/> is exactly 0, returns false
    /// and sets every element of <paramref name="result"/> to NaN.
    /// </summary>
    public static bool TryInvert(Matrix2 matrix, out Matrix2 result)
    {
        var determinant = matrix.Determinant;
        if (determinant == 0f)
        {
            result = FromColumns(new Vector2(float.NaN), new Vector2(float.NaN));
            return false;
        }

        var inverseDeterminant = 1f / determinant;

        // Adjugate columns (m11, -m10) and (-m01, m00). The negations are subtractions from zero, so a zero cofactor
        // stays +0, as the 3 × 3 and 4 × 4 cofactor sums give: the inverse of a diagonal matrix is diagonal bit for bit.
        result = FromColumns(
            new Vector2(matrix.Column1.Y, 0f - matrix.Column0.Y) * inverseDeterminant,
            new Vector2(0f - matrix.Column1.X, matrix.Column0.X) * inverseDeterminant);
        return true;
    }

    /// <summary>The rotation by <paramref name="angle"/> radians: rows <c>(c, -s) (s, c)</c>, turning +X toward +Y.</summary>
    public static Matrix2 CreateRotation(float angle)
    {
        var (sin, cos) = MathF.SinCos(angle);
        return FromColumns(new Vector2(cos, sin), new Vector2(-sin, cos));
    }

    /// <summary>The uniform scale: <paramref name="scale"/> on the diagonal.</summary>
    public static Matrix2 CreateScale(float scale)
    {
        return CreateScale(scale, scale);
    }

    /// <summary>The scale with <paramref name="scale"/>'s components on the diagonal.</summary>
    public static Matrix2 CreateScale(Vector2 scale)
    {
        return CreateScale(scale.X, scale.Y);
    }

    /// <summary>The scale with <paramref name="x"/>, <paramref name="y"/> on the diagonal.</summary>
    public static Matrix2 CreateScale(float x, float y)
    {
        return FromColumns(new Vector2(x, 0f), new Vector2(0f, y));
    }

    /// <summary>The column-vector product: <c>(left * right) * v == left * (right * v)</c>, so <paramref name="right"/> applies first.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix2 operator *(Matrix2 left, Matrix2 right)
    {
        return FromColumns(left * right.Column0, left * right.Column1);
    }

    /// <summary><c>M * v</c>: <c>Column0 * v.X + Column1 * v.Y</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 operator *(Matrix2 matrix, Vector2 vector)
    {
        // AsVector128 zeroes the two unused lanes; AsVector2 reads back the first two.
        var result = matrix.Column0.AsVector128() * vector.X;
        result = Vector128.MultiplyAddEstimate(matrix.Column1.AsVector128(), Vector128.Create(vector.Y), result);
        return result.AsVector2();
    }

    /// <summary>IEEE comparison per element: NaN differs from everything, +0 equals -0.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Matrix2 left, Matrix2 right)
    {
        return left.Column0 == right.Column0 && left.Column1 == right.Column1;
    }

    /// <summary>The negation of <c>==</c>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Matrix2 left, Matrix2 right)
    {
        return !(left == right);
    }

    /// <summary><see cref="float.Equals(float)"/> per element: NaN equals NaN, +0 equals -0.</summary>
    public readonly bool Equals(Matrix2 other)
    {
        return Column0.Equals(other.Column0) && Column1.Equals(other.Column1);
    }

    public override readonly bool Equals(object? obj)
    {
        return obj is Matrix2 other && Equals(other);
    }

    /// <summary>Combines the columns' hashes, which agree with <see cref="Equals(Matrix2)"/> per element.</summary>
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
