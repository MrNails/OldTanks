namespace CoolEngine.Imaging.Jpeg;

/// <summary>
/// The 8×8 inverse DCT with dequantization: libjpeg's accurate integer <c>jpeg_idct_islow</c> (Loeffler–Ligtenberg–Moschytz),
/// so the output matches the libjpeg family of decoders sample for sample.
/// </summary>
internal static class Idct
{
    private const int ConstBits = 13;
    private const int Pass1Bits = 2;
    private const int CenterSample = 128;

    // Fixed-point constants, value × 2^13 rounded.
    private const int Fix0298631336 = 2446;
    private const int Fix0390180644 = 3196;
    private const int Fix0541196100 = 4433;
    private const int Fix0765366865 = 6270;
    private const int Fix0899976223 = 7373;
    private const int Fix1175875602 = 9633;
    private const int Fix1501321110 = 12299;
    private const int Fix1847759065 = 15137;
    private const int Fix1961570560 = 16069;
    private const int Fix2053119869 = 16819;
    private const int Fix2562915447 = 20995;
    private const int Fix3072711026 = 25172;

    /// <summary>Dequantizes one block and writes its 8×8 samples, offset by 128 and clamped to 0..255.</summary>
    /// <param name="coefficients">64 coefficients in natural (row-major) order.</param>
    /// <param name="quantization">64 quantization steps in natural order.</param>
    /// <param name="output">Receives row <c>r</c>, column <c>c</c> at <c>r * stride + c</c>.</param>
    /// <param name="stride">The distance between output rows.</param>
    /// <remarks>Arithmetic is unchecked: garbage coefficients from corrupt data give garbage samples, never an exception.</remarks>
    public static void Transform(ReadOnlySpan<short> coefficients, ReadOnlySpan<ushort> quantization, Span<byte> output, int stride)
    {
        if (coefficients.Length < 64 || quantization.Length < 64)
            throw new ArgumentException("A block needs 64 coefficients and 64 quantization steps.");

        if (stride < 8 || output.Length < 7 * stride + 8)
            throw new ArgumentException("The output is too small for an 8x8 block at this stride.", nameof(output));

        Span<int> workspace = stackalloc int[64];

        unchecked
        {
            // Pass 1: columns from the input into the workspace, scaled up by 2^Pass1Bits.
            for (var column = 0; column < 8; column++)
            {
                if (coefficients[8 + column] == 0 && coefficients[16 + column] == 0 && coefficients[24 + column] == 0 &&
                    coefficients[32 + column] == 0 && coefficients[40 + column] == 0 && coefficients[48 + column] == 0 &&
                    coefficients[56 + column] == 0)
                {
                    // Only the DC term: the column is constant.
                    var dc = coefficients[column] * quantization[column] << Pass1Bits;
                    for (var row = 0; row < 8; row++)
                        workspace[row * 8 + column] = dc;

                    continue;
                }

                // Even part.
                var z2 = coefficients[16 + column] * quantization[16 + column];
                var z3 = coefficients[48 + column] * quantization[48 + column];

                var z1 = (z2 + z3) * Fix0541196100;
                var tmp2 = z1 + z3 * -Fix1847759065;
                var tmp3 = z1 + z2 * Fix0765366865;

                z2 = coefficients[column] * quantization[column];
                z3 = coefficients[32 + column] * quantization[32 + column];

                var tmp0 = (z2 + z3) << ConstBits;
                var tmp1 = (z2 - z3) << ConstBits;

                var tmp10 = tmp0 + tmp3;
                var tmp13 = tmp0 - tmp3;
                var tmp11 = tmp1 + tmp2;
                var tmp12 = tmp1 - tmp2;

                // Odd part.
                tmp0 = coefficients[56 + column] * quantization[56 + column];
                tmp1 = coefficients[40 + column] * quantization[40 + column];
                tmp2 = coefficients[24 + column] * quantization[24 + column];
                tmp3 = coefficients[8 + column] * quantization[8 + column];

                OddPart(ref tmp0, ref tmp1, ref tmp2, ref tmp3);

                const int shift = ConstBits - Pass1Bits;
                workspace[column] = Descale(tmp10 + tmp3, shift);
                workspace[56 + column] = Descale(tmp10 - tmp3, shift);
                workspace[8 + column] = Descale(tmp11 + tmp2, shift);
                workspace[48 + column] = Descale(tmp11 - tmp2, shift);
                workspace[16 + column] = Descale(tmp12 + tmp1, shift);
                workspace[40 + column] = Descale(tmp12 - tmp1, shift);
                workspace[24 + column] = Descale(tmp13 + tmp0, shift);
                workspace[32 + column] = Descale(tmp13 - tmp0, shift);
            }

            // Pass 2: rows from the workspace to the output, removing both scale factors and the 2^3 of the 2-D transform.
            for (var row = 0; row < 8; row++)
            {
                var ws = workspace.Slice(row * 8, 8);
                var outRow = output.Slice(row * stride, 8);

                if (ws[1] == 0 && ws[2] == 0 && ws[3] == 0 && ws[4] == 0 && ws[5] == 0 && ws[6] == 0 && ws[7] == 0)
                {
                    outRow.Fill(Clamp(Descale(ws[0], Pass1Bits + 3)));
                    continue;
                }

                // Even part.
                var z2 = ws[2];
                var z3 = ws[6];

                var z1 = (z2 + z3) * Fix0541196100;
                var tmp2 = z1 + z3 * -Fix1847759065;
                var tmp3 = z1 + z2 * Fix0765366865;

                var tmp0 = (ws[0] + ws[4]) << ConstBits;
                var tmp1 = (ws[0] - ws[4]) << ConstBits;

                var tmp10 = tmp0 + tmp3;
                var tmp13 = tmp0 - tmp3;
                var tmp11 = tmp1 + tmp2;
                var tmp12 = tmp1 - tmp2;

                // Odd part.
                tmp0 = ws[7];
                tmp1 = ws[5];
                tmp2 = ws[3];
                tmp3 = ws[1];

                OddPart(ref tmp0, ref tmp1, ref tmp2, ref tmp3);

                const int shift = ConstBits + Pass1Bits + 3;
                outRow[0] = Clamp(Descale(tmp10 + tmp3, shift));
                outRow[7] = Clamp(Descale(tmp10 - tmp3, shift));
                outRow[1] = Clamp(Descale(tmp11 + tmp2, shift));
                outRow[6] = Clamp(Descale(tmp11 - tmp2, shift));
                outRow[2] = Clamp(Descale(tmp12 + tmp1, shift));
                outRow[5] = Clamp(Descale(tmp12 - tmp1, shift));
                outRow[3] = Clamp(Descale(tmp13 + tmp0, shift));
                outRow[4] = Clamp(Descale(tmp13 - tmp0, shift));
            }
        }
    }

    /// <summary>The odd part shared by both passes: in, the inputs at positions 7, 5, 3, 1; out, the four odd-part terms.</summary>
    private static void OddPart(ref int tmp0, ref int tmp1, ref int tmp2, ref int tmp3)
    {
        unchecked
        {
            var z1 = tmp0 + tmp3;
            var z2 = tmp1 + tmp2;
            var z3 = tmp0 + tmp2;
            var z4 = tmp1 + tmp3;
            var z5 = (z3 + z4) * Fix1175875602;

            tmp0 *= Fix0298631336;
            tmp1 *= Fix2053119869;
            tmp2 *= Fix3072711026;
            tmp3 *= Fix1501321110;
            z1 *= -Fix0899976223;
            z2 *= -Fix2562915447;
            z3 *= -Fix1961570560;
            z4 *= -Fix0390180644;

            z3 += z5;
            z4 += z5;

            tmp0 += z1 + z3;
            tmp1 += z2 + z4;
            tmp2 += z2 + z3;
            tmp3 += z1 + z4;
        }
    }

    private static int Descale(int value, int shift) => unchecked(value + (1 << (shift - 1))) >> shift;

    private static byte Clamp(int value) => (byte)Math.Clamp(value + CenterSample, 0, 255);
}
