// Follows FreeType 2.13.2 src/base/ftcalc.c (FT_MulDiv, FT_MulDiv_No_Round, FT_DivFix, FT_Vector_NormLen) and the
// TT_MulFix14 and TT_DotFix14 helpers of src/truetype/ttinterp.c, for a build whose FT_Long is 64 bits wide (FreeTypeSharp's
// native freetype.dll) (FreeType Project License; credit in docs).

using System.Numerics;

namespace CoolEngine.Fonts.Hinting.TrueType;

/// <summary>
/// The fixed-point arithmetic of FreeType 2.13.2's TrueType interpreter, with its rounding and its wrap-around: products and
/// sums wrap at 64 bits as the C code's unsigned casts make them (C# <c>long</c> arithmetic is unchecked by default).
/// </summary>
internal static class HintingMath
{
    /// <summary><c>FT_MulDiv</c>: <c>a * b / c</c> rounded half away from zero; <c>0x7FFFFFFF</c> with the sign for c = 0.</summary>
    public static long MulDiv(long a, long b, long c)
    {
        var s = 1;
        var ua = (ulong)a;
        var ub = (ulong)b;
        var uc = (ulong)c;
        if (a < 0)
        {
            ua = 0 - ua;
            s = -s;
        }

        if (b < 0)
        {
            ub = 0 - ub;
            s = -s;
        }

        if (c < 0)
        {
            uc = 0 - uc;
            s = -s;
        }

        var d = uc > 0 ? (ua * ub + (uc >> 1)) / uc : 0x7FFFFFFFUL;
        var result = (long)d;

        return s < 0 ? (long)(0 - (ulong)result) : result;
    }

    /// <summary><c>FT_MulDiv_No_Round</c>: <c>a * b / c</c> truncated; <c>0x7FFFFFFF</c> with the sign for c = 0.</summary>
    public static long MulDivNoRound(long a, long b, long c)
    {
        var s = 1;
        var ua = (ulong)a;
        var ub = (ulong)b;
        var uc = (ulong)c;
        if (a < 0)
        {
            ua = 0 - ua;
            s = -s;
        }

        if (b < 0)
        {
            ub = 0 - ub;
            s = -s;
        }

        if (c < 0)
        {
            uc = 0 - uc;
            s = -s;
        }

        var d = uc > 0 ? ua * ub / uc : 0x7FFFFFFFUL;
        var result = (long)d;

        return s < 0 ? (long)(0 - (ulong)result) : result;
    }

    /// <summary><c>FT_DivFix</c>: <c>a * 65536 / b</c> rounded half away from zero; <c>0x7FFFFFFF</c> with the sign for b = 0.</summary>
    public static long DivFix(long a, long b)
    {
        var s = 1;
        var ua = (ulong)a;
        var ub = (ulong)b;
        if (a < 0)
        {
            ua = 0 - ua;
            s = -s;
        }

        if (b < 0)
        {
            ub = 0 - ub;
            s = -s;
        }

        var q = ub > 0 ? ((ua << 16) + (ub >> 1)) / ub : 0x7FFFFFFFUL;
        var result = (long)q;

        return s < 0 ? (long)(0 - (ulong)result) : result;
    }

    /// <summary><c>FT_MulFix</c>: <c>a * b / 65536</c> rounded half away from zero.</summary>
    public static long MulFix(long a, long b)
    {
        var ab = a * b;
        return (ab + 0x8000L - (ab < 0 ? 1 : 0)) >> 16;
    }

    /// <summary><c>TT_MulFix14</c>: <c>a * b / 16384</c> rounded half away from zero, <paramref name="a"/> taken as 32 bits.</summary>
    public static long MulFix14(long a, int b)
    {
        var ab = (long)(int)a * b;
        ab += 0x2000 + (ab >> 63);
        return (int)(ab >> 14);
    }

    /// <summary><c>TT_DotFix14</c>: the dot product of (ax, ay), taken as 32 bits, and a 2.14 vector, rounded.</summary>
    public static long DotFix14(long ax, long ay, int bx, int by)
    {
        var temp1 = (long)(int)ax * bx;
        var temp2 = (long)(int)ay * by;
        temp1 += temp2;
        temp2 = temp1 >> 63;
        temp1 += 0x2000 + temp2;
        return (int)(temp1 >> 14);
    }

    /// <summary>
    /// <c>FT_Vector_NormLen</c>: scales (x, y) to unit length in 16.16 in place, with FreeType's Newton iteration (both taken as
    /// 32 bits).
    /// </summary>
    public static void NormLen(ref long vx, ref long vy)
    {
        var xs = (int)vx;
        var ys = (int)vy;
        var x = (uint)xs;
        var y = (uint)ys;
        int sx = 1, sy = 1;
        if (xs < 0)
        {
            x = 0u - x;
            sx = -1;
        }

        if (ys < 0)
        {
            y = 0u - y;
            sy = -1;
        }

        if (x == 0)
        {
            if (y > 0)
                vy = sy * 0x10000L;

            return;
        }

        if (y == 0)
        {
            if (x > 0)
                vx = sx * 0x10000L;

            return;
        }

        var l = x > y ? x + (y >> 1) : y + (x >> 1);
        var shift = 31 - (31 - BitOperations.LeadingZeroCount(l));
        shift -= 15 + (l >= (0xAAAAAAAAU >> shift) ? 1 : 0);

        if (shift > 0)
        {
            x <<= shift;
            y <<= shift;

            // Re-estimate the length for tiny vectors.
            l = x > y ? x + (y >> 1) : y + (x >> 1);
        }
        else
        {
            x >>= -shift;
            y >>= -shift;
            l >>= -shift;
        }

        var b = 0x10000 - (int)l;
        var xi = (int)x;
        var yi = (int)y;
        uint u, v;
        int z;
        do
        {
            u = (uint)(xi + ((xi * b) >> 16));
            v = (uint)(yi + ((yi * b) >> 16));

            z = -(int)(u * u + v * v) / 0x200;
            z = z * ((0x10000 + b) >> 8) / 0x10000;

            b += z;
        } while (z > 0);

        vx = sx < 0 ? -(long)u : u;
        vy = sy < 0 ? -(long)v : v;
    }
}
