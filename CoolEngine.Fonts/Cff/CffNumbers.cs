// Follows FreeType 2.13.2 src/cff/cffparse.c (cff_parse_integer, cff_parse_real, do_fixed, cff_parse_fixed_dynamic,
// cff_parse_font_matrix) as the reference DLL builds them, with a 64-bit FT_Long (FreeType Project License; credit in docs).

namespace CoolEngine.Fonts.Cff;

/// <summary>
/// The numbers of a CFF or CFF2 DICT, decoded with FreeType 2.13.2's rounding, so the font matrix (the one real-valued DICT
/// entry outlines depend on) is the same matrix FreeType applies. Values are 64-bit, as the reference's <c>FT_Long</c>.
/// </summary>
internal static class CffNumbers
{
    private static readonly long[] s_powerTens = [1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000];

    /// <summary>The value of an integer operand (bytes 28, 29, 32..254) starting at <paramref name="at"/>, which the
    /// tokenizer has checked lies wholly inside <paramref name="dict"/>. A 29-encoded value is read as FreeType's 64-bit
    /// <c>cff_parse_integer</c> reads it: unsigned, so its top bit does not make it negative.</summary>
    public static long ParseInteger(ReadOnlySpan<byte> dict, int at)
    {
        int v = dict[at];
        return v switch
        {
            28 => (short)(dict[at + 1] << 8 | dict[at + 2]),
            29 => (long)(uint)(dict[at + 1] << 24 | dict[at + 2] << 16 | dict[at + 3] << 8 | dict[at + 4]),
            < 247 => v - 139,
            < 251 => (v - 247) * 256 + dict[at + 1] + 108,
            _ => -(v - 251) * 256 - dict[at + 1] - 108,
        };
    }

    /// <summary>
    /// <c>cff_parse_real</c>: the real whose 0x1E byte is at <paramref name="start"/>, in 16.16 (scaled by 10^<paramref name="powerTen"/>);
    /// with <paramref name="dynamic"/>, the dynamic form whose power of ten goes to <paramref name="scaling"/>.
    /// </summary>
    public static long ParseReal(ReadOnlySpan<byte> dict, int start, int powerTen, bool dynamic, out int scaling)
    {
        scaling = 0;
        var p = start;
        int nib;
        var phase = 4;
        var sign = false;
        var exponentSign = false;
        var haveOverflow = false;
        long number = 0, exponent = 0, exponentAdd = 0, integerLength = 0, fractionLength = 0;
        long result;

        // The integer part.
        for (;;)
        {
            if (phase != 0)
            {
                p++;
                if (p >= dict.Length)
                    return 0;
            }

            nib = (dict[p] >> phase) & 0xF;
            phase = 4 - phase;

            if (nib == 0xE)
            {
                sign = true;
            }
            else if (nib > 9)
            {
                break;
            }
            else if (number >= 0xCCCCCCC)
            {
                exponentAdd++;
            }
            else if (nib != 0 || number != 0)
            {
                integerLength++;
                number = number * 10 + nib;
            }
        }

        // The fraction part.
        if (nib == 0xA)
        {
            for (;;)
            {
                if (phase != 0)
                {
                    p++;
                    if (p >= dict.Length)
                        return 0;
                }

                nib = (dict[p] >> phase) & 0xF;
                phase = 4 - phase;
                if (nib > 9)
                    break;

                if (nib == 0 && number == 0)
                {
                    exponentAdd--;
                }
                else if (number < 0xCCCCCCC && fractionLength < 9)
                {
                    fractionLength++;
                    number = number * 10 + nib;
                }
            }
        }

        // The exponent.
        if (nib == 12)
        {
            exponentSign = true;
            nib = 11;
        }

        if (nib == 11)
        {
            for (;;)
            {
                if (phase != 0)
                {
                    p++;
                    if (p >= dict.Length)
                        return 0;
                }

                nib = (dict[p] >> phase) & 0xF;
                phase = 4 - phase;
                if (nib > 9)
                    break;

                if (exponent > 1000)
                    haveOverflow = true;
                else
                    exponent = exponent * 10 + nib;
            }

            if (exponentSign)
                exponent = -exponent;
        }

        if (number == 0)
            return 0;

        if (haveOverflow)
            return Signed(exponentSign ? 0 : 0x7FFFFFFF, sign);

        exponent += powerTen + exponentAdd;

        if (dynamic)
        {
            fractionLength += integerLength;
            exponent += integerLength;

            if (fractionLength <= 5)
            {
                if (number > 0x7FFF)
                {
                    result = CffFixed.DivFix(number, 10);
                    scaling = (int)(exponent - fractionLength + 1);
                }
                else
                {
                    if (exponent > 0)
                    {
                        var newFractionLength = Math.Min(exponent, 5);
                        var shift = newFractionLength - fractionLength;
                        if (shift > 0)
                        {
                            exponent -= newFractionLength;
                            number *= s_powerTens[shift];
                            if (number > 0x7FFF)
                            {
                                number /= 10;
                                exponent += 1;
                            }
                        }
                        else
                        {
                            exponent -= fractionLength;
                        }
                    }
                    else
                    {
                        exponent -= fractionLength;
                    }

                    result = (long)((ulong)number << 16);
                    scaling = (int)exponent;
                }
            }
            else if (number / s_powerTens[fractionLength - 5] > 0x7FFF)
            {
                result = CffFixed.DivFix(number, s_powerTens[fractionLength - 4]);
                scaling = (int)(exponent - 4);
            }
            else
            {
                result = CffFixed.DivFix(number, s_powerTens[fractionLength - 5]);
                scaling = (int)(exponent - 5);
            }
        }
        else
        {
            integerLength += exponent;
            fractionLength -= exponent;

            if (integerLength > 5)
                return Signed(0x7FFFFFFF, sign);

            if (integerLength < -5)
                return 0;

            // Remove the digits that cannot count.
            if (integerLength < 0)
            {
                number /= s_powerTens[-integerLength];
                fractionLength += integerLength;
            }

            // This can only happen if the exponent was non-zero.
            if (fractionLength == 10)
            {
                number /= 10;
                fractionLength -= 1;
            }

            if (fractionLength > 0)
            {
                if (number / s_powerTens[fractionLength] > 0x7FFF)
                    return 0;

                result = CffFixed.DivFix(number, s_powerTens[fractionLength]);
            }
            else
            {
                number *= s_powerTens[-fractionLength];
                if (number > 0x7FFF)
                    return Signed(0x7FFFFFFF, sign);

                result = (long)((ulong)number << 16);
            }
        }

        return Signed(result, sign);
    }

    /// <summary><c>cff_parse_fixed</c> (<c>do_fixed</c> with no scaling): a 16.16 value of an integer, real or blended operand.</summary>
    public static long ParseFixed(ReadOnlySpan<byte> dict, in DictOperand operand)
    {
        switch (operand.Kind)
        {
            case DictOperandKind.Blended:
                return operand.Fixed;

            case DictOperandKind.Real:
                return ParseReal(dict, operand.Position, 0, false, out _);

            default:
                var value = ParseInteger(dict, operand.Position);
                if (value > 0x7FFF)
                    return 0x7FFFFFFF;

                if (value < -0x7FFF)
                    return -0x7FFFFFFF;

                return (long)((ulong)value << 16);
        }
    }

    /// <summary><c>cff_parse_fixed_dynamic</c>: a 16.16 value and the power of ten that scales it.</summary>
    public static long ParseFixedDynamic(ReadOnlySpan<byte> dict, in DictOperand operand, out int scaling)
    {
        if (operand.Kind == DictOperandKind.Real)
            return ParseReal(dict, operand.Position, 0, true, out scaling);

        var number = ParseInteger(dict, operand.Position);
        if (number > 0x7FFF)
        {
            int integerLength;
            for (integerLength = 5; integerLength < 10; integerLength++)
            {
                if (number < s_powerTens[integerLength])
                    break;
            }

            if (number / s_powerTens[integerLength - 5] > 0x7FFF)
            {
                scaling = integerLength - 4;
                return CffFixed.DivFix(number, s_powerTens[integerLength - 4]);
            }

            scaling = integerLength - 5;
            return CffFixed.DivFix(number, s_powerTens[integerLength - 5]);
        }

        scaling = 0;
        return (long)((ulong)number << 16);
    }

    /// <summary>
    /// <c>cff_parse_font_matrix</c>: the matrix, the offset (16.16, not yet in font units) and the units per em of a
    /// <c>FontMatrix</c> entry's six operands; FreeType's default (identity, upm 1) when the values are unlikely.
    /// </summary>
    public static (CffMatrix Matrix, long OffsetX, long OffsetY, long UnitsPerEm) ParseFontMatrix(ReadOnlySpan<byte> dict,
        ReadOnlySpan<DictOperand> operands)
    {
        Span<long> values = stackalloc long[6];
        Span<int> scalings = stackalloc int[6];
        var maxScaling = int.MinValue;
        var minScaling = int.MaxValue;
        for (var i = 0; i < 6; i++)
        {
            values[i] = ParseFixedDynamic(dict, operands[i], out scalings[i]);
            if (values[i] == 0)
                continue;

            maxScaling = Math.Max(maxScaling, scalings[i]);
            minScaling = Math.Min(minScaling, scalings[i]);
        }

        var unlikely = (CffMatrix.Identity, 0L, 0L, 1L);
        if (maxScaling is < -9 or > 0 || (long)maxScaling - minScaling is < 0 or > 9)
            return unlikely;

        for (var i = 0; i < 6; i++)
        {
            var value = values[i];
            if (value == 0)
                continue;

            var divisor = s_powerTens[maxScaling - scalings[i]];
            var halfDivisor = divisor >> 1;
            if (value < 0)
                values[i] = long.MinValue + halfDivisor < value ? (value - halfDivisor) / divisor : long.MinValue / divisor;
            else
                values[i] = long.MaxValue - halfDivisor > value ? (value + halfDivisor) / divisor : long.MaxValue / divisor;
        }

        var matrix = new CffMatrix(values[0], values[2], values[1], values[3]);
        if (!CffFixed.MatrixCheck(matrix))
            return unlikely;

        return (matrix, values[4], values[5], s_powerTens[-maxScaling]);
    }

    private static long Signed(long value, bool negative) => negative ? -value : value;
}
