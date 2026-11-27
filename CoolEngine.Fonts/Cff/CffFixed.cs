// Follows FreeType 2.13.2 src/base/ftcalc.c (FT_MulFix, FT_DivFix, FT_MulDiv, FT_Matrix_Check, FT_Matrix_Multiply_Scaled,
// FT_Vector_Transform_Scaled) and the FT_ABS and NEG_INT32 macros (FreeType Project License; credit in docs).

namespace CoolEngine.Fonts.Cff;

/// <summary>
/// FreeType 2.13.2's fixed-point arithmetic as the reference DLL computes it. Its <c>FT_Long</c>, <c>FT_Fixed</c> and
/// <c>FT_Pos</c> are 64 bits (FreeTypeSharp binds them as <c>nint</c>; a 32-bit build would read none of its records), so the
/// font matrix, the scales and the outline coordinates are 64-bit values: products and quotients in 64-bit integers that wrap,
/// as FreeType's <c>FT_Int64</c> ones do. The Type 2 interpreter (<c>psintrp.c</c>) computes in 32-bit <c>CF2_Fixed</c>
/// numbers instead: its results of <c>FT_MulFix</c> and <c>FT_DivFix</c> are truncated to 32 bits (<see cref="MulFix32"/>,
/// <see cref="DivFix32"/>) and its absolute value wraps at 32 bits (<see cref="Abs32"/>).
/// </summary>
internal static class CffFixed
{
    /// <summary>1.0 in 16.16.</summary>
    public const int One = 0x10000;

    /// <summary><c>FT_MulFix</c>: <c>a * b / 65536</c> rounded half away from zero (the product wraps at 64 bits).</summary>
    public static long MulFix(long a, long b)
    {
        var ab = a * b;
        return (ab + 0x8000L - (ab < 0 ? 1 : 0)) >> 16;
    }

    /// <summary><c>FT_DivFix</c>: <c>a * 65536 / b</c> rounded half away from zero, <c>0x7FFFFFFF</c> (signed) for b = 0.</summary>
    public static long DivFix(long a, long b)
    {
        var negative = false;
        var ua = (ulong)a;
        var ub = (ulong)b;
        if (a < 0)
        {
            ua = 0 - ua;
            negative = !negative;
        }

        if (b < 0)
        {
            ub = 0 - ub;
            negative = !negative;
        }

        var q = ub > 0 ? ((ua << 16) + (ub >> 1)) / ub : 0x7FFFFFFFUL;
        return negative ? (long)(0 - q) : (long)q;
    }

    /// <summary><c>FT_MulDiv</c>: <c>a * b / c</c> rounded half away from zero, <c>0x7FFFFFFF</c> (signed) for c = 0.</summary>
    public static long MulDiv(long a, long b, long c)
    {
        var negative = false;
        var ua = (ulong)a;
        var ub = (ulong)b;
        var uc = (ulong)c;
        if (a < 0)
        {
            ua = 0 - ua;
            negative = !negative;
        }

        if (b < 0)
        {
            ub = 0 - ub;
            negative = !negative;
        }

        if (c < 0)
        {
            uc = 0 - uc;
            negative = !negative;
        }

        var d = uc > 0 ? (ua * ub + (uc >> 1)) / uc : 0x7FFFFFFFUL;
        return negative ? (long)(0 - d) : (long)d;
    }

    /// <summary><c>FT_MulFix</c> as the Type 2 interpreter uses it: 32-bit operands, the result stored in a 32-bit <c>CF2_Fixed</c>.</summary>
    public static int MulFix32(int a, int b) => (int)MulFix(a, b);

    /// <summary><c>FT_DivFix</c> as the Type 2 interpreter uses it: 32-bit operands, the result stored in a 32-bit <c>CF2_Fixed</c>.</summary>
    public static int DivFix32(int a, int b) => (int)DivFix(a, b);

    /// <summary><c>FT_ABS</c> on a 64-bit <c>FT_Long</c>, wrapping (only long.MinValue, which no font value reaches, stays negative).</summary>
    public static long Abs(long value) => value < 0 ? (long)(0 - (ulong)value) : value;

    /// <summary>
    /// The interpreter's 32-bit absolute value (<c>cf2_fixedAbs</c>, through <c>NEG_INT32</c>): int.MinValue stays int.MinValue,
    /// where <see cref="Math.Abs(int)"/> would throw.
    /// </summary>
    public static int Abs32(int value) => value < 0 ? (int)(0u - (uint)value) : value;

    /// <summary>
    /// <c>FT_Matrix_Check</c>: false for a matrix FreeType considers degenerate (all zero, an element past 32 bits, or a
    /// determinant too small for its elements).
    /// </summary>
    public static bool MatrixCheck(in CffMatrix matrix)
    {
        long xx = matrix.Xx, xy = matrix.Xy, yx = matrix.Yx, yy = matrix.Yy;
        var value = Abs(xx) | Abs(xy) | Abs(yx) | Abs(yy);
        if (value == 0 || value > 0x7FFFFFFFL)
            return false;

        // Scale so the products below fit, as FreeType does: the largest element keeps 13 significant bits.
        var shift = (31 - System.Numerics.BitOperations.LeadingZeroCount((uint)value)) - 12;
        if (shift > 0)
        {
            xx >>= shift;
            xy >>= shift;
            yx >>= shift;
            yy >>= shift;
        }

        var temp1 = 32UL * (ulong)Abs(xx * yy - xy * yx);
        var temp2 = (ulong)(xx * xx) + (ulong)(xy * xy) + (ulong)(yx * yx) + (ulong)(yy * yy);

        return temp1 > temp2;
    }

    /// <summary><c>FT_Matrix_Multiply_Scaled(a, b, scaling)</c>: <c>b = a × b / scaling</c>.</summary>
    public static CffMatrix MultiplyScaled(in CffMatrix a, in CffMatrix b, long scaling)
    {
        var value = One * scaling;
        return new CffMatrix(
            MulDiv(a.Xx, b.Xx, value) + MulDiv(a.Xy, b.Yx, value),
            MulDiv(a.Xx, b.Xy, value) + MulDiv(a.Xy, b.Yy, value),
            MulDiv(a.Yx, b.Xx, value) + MulDiv(a.Yy, b.Yx, value),
            MulDiv(a.Yx, b.Xy, value) + MulDiv(a.Yy, b.Yy, value));
    }

    /// <summary><c>FT_Vector_Transform_Scaled(vector, matrix, scaling)</c>.</summary>
    public static (long X, long Y) TransformScaled(long x, long y, in CffMatrix matrix, long scaling)
    {
        var value = One * scaling;
        return (MulDiv(x, matrix.Xx, value) + MulDiv(y, matrix.Xy, value),
            MulDiv(x, matrix.Yx, value) + MulDiv(y, matrix.Yy, value));
    }
}

/// <summary>A 16.16 2 × 2 matrix as FreeType's <c>FT_Matrix</c> (64-bit elements): x' = xx·x + xy·y, y' = yx·x + yy·y.</summary>
internal readonly record struct CffMatrix(long Xx, long Xy, long Yx, long Yy)
{
    public static CffMatrix Identity => new(CffFixed.One, 0, 0, CffFixed.One);

    public bool IsIdentity => Xx == CffFixed.One && Yy == CffFixed.One && Xy == 0 && Yx == 0;
}
