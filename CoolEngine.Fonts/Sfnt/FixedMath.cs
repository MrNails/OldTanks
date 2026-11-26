// Follows FreeType 2.13.2 src/base/ftcalc.c (FT_MulFix, FT_DivFix), src/base/fttrigon.c (FT_Vector_Length, behind
// FT_Hypot) and the FT_PIX_* macros of include/freetype/internal/ftobjs.h (FreeType Project License; credit in docs).

namespace CoolEngine.Fonts.Sfnt;

/// <summary>
/// Fixed-point arithmetic with FreeType 2.13.2's rounding (<c>ftcalc.c</c>, <c>fttrigon.c</c>, the <c>FT_PIX_*</c> macros), so
/// scaled values match FreeType's to the unit. 16.16 values are scales and matrices; 26.6 values are pixel coordinates.
/// </summary>
internal static class FixedMath
{
    // The CORDIC shrink factor 0.858785336480436 * 2^32, the highest safe bit and the iteration count of FreeType's
    // FT_Vector_Length.
    private const ulong TrigScale = 0xDBD95B16UL;
    private const int TrigSafeMsb = 29;
    private const int TrigMaxIterations = 23;

    /// <summary><c>FT_MulFix</c>: <c>a * b / 65536</c>, rounded half away from zero.</summary>
    public static long MulFix(long a, long b)
    {
        var ab = a * b;

        return (ab + 0x8000L - (ab < 0 ? 1 : 0)) >> 16;
    }

    /// <summary><c>FT_DivFix</c>: <c>a * 65536 / b</c>, rounded half away from zero; <c>0x7FFFFFFF</c> with the sign for b = 0.</summary>
    public static long DivFix(long a, long b)
    {
        var negative = (a < 0) != (b < 0);
        var ua = (ulong)Math.Abs(a);
        var ub = (ulong)Math.Abs(b);
        var q = ub > 0 ? ((ua << 16) + (ub >> 1)) / ub : 0x7FFFFFFFUL;

        return negative ? -(long)q : (long)q;
    }

    /// <summary><c>FT_PIX_ROUND</c> of a 26.6 value.</summary>
    public static long PixRound(long x) => (x + 32) & ~63L;

    /// <summary><c>FT_PIX_CEIL</c> of a 26.6 value.</summary>
    public static long PixCeil(long x) => (x + 63) & ~63L;

    /// <summary><c>FT_PIX_FLOOR</c> of a 26.6 value.</summary>
    public static long PixFloor(long x) => x & ~63L;

    /// <summary><c>FT_Hypot</c> (<c>FT_Vector_Length</c>): the CORDIC length of a vector, as FreeType computes it.</summary>
    public static long Hypot(long x, long y)
    {
        if (x == 0)
            return Math.Abs(y);

        if (y == 0)
            return Math.Abs(x);

        // Normalise so the larger component's highest bit is bit 29.
        var msb = 31 - System.Numerics.BitOperations.LeadingZeroCount((uint)(Math.Abs(x) | Math.Abs(y)));
        int shift;
        if (msb <= TrigSafeMsb)
        {
            shift = TrigSafeMsb - msb;
            x = (int)((uint)x << shift);
            y = (int)((uint)y << shift);
        }
        else
        {
            shift = msb - TrigSafeMsb;
            x >>= shift;
            y >>= shift;
            shift = -shift;
        }

        // Pseudo-polarise: rotate into [-pi/4, pi/4], then the CORDIC pseudo-rotations; only the length is kept.
        if (y > x)
        {
            if (y > -x)
                (x, y) = (y, -x);
            else
                (x, y) = (-x, -y);
        }
        else if (y < -x)
        {
            (x, y) = (-y, x);
        }

        for (int i = 1, b = 1; i < TrigMaxIterations; b <<= 1, i++)
        {
            long xTemp;
            if (y > 0)
            {
                xTemp = x + ((y + b) >> i);
                y -= (x + b) >> i;
            }
            else
            {
                xTemp = x - ((y + b) >> i);
                y += (x + b) >> i;
            }

            x = xTemp;
        }

        // Downscale by the CORDIC gain.
        var negative = x < 0;
        var length = (long)(((ulong)Math.Abs(x) * TrigScale + 0x40000000UL) >> 32);
        if (negative)
            length = -length;

        if (shift > 0)
            return (length + (1L << (shift - 1))) >> shift;

        return (long)((uint)length << -shift);
    }
}
