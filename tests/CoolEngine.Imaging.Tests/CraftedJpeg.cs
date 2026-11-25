namespace CoolEngine.Imaging.Tests;

/// <summary>
/// Writes small JPEGs bit by bit, for files ImageSharp cannot encode: a sparse DC Huffman table, any sampling factors,
/// restart intervals, a scan cut short, and progressive files with any scan script. Every block is DC-only (sequential) or
/// all zero (progressive) unless the caller gives AC symbols; quantization is 8 everywhere.
/// </summary>
internal static class CraftedJpeg
{
    private const byte Sof0 = 0xC0;
    private const byte Sof2 = 0xC2;
    private const byte Dht = 0xC4;
    private const byte Sos = 0xDA;
    private const byte Dqt = 0xDB;
    private const byte Dri = 0xDD;
    private const byte App0 = 0xE0;

    // The progressive AC table's largest EOB run (EOB14: 2^14 + 14 extra bits of ones).
    private const int MaxEobRun = 32767;

    /// <summary>One frame component; <paramref name="Sample"/> gives the block's level (0..255) from its block row and column.</summary>
    internal readonly record struct Component(byte Id, int H, int V, Func<int, int, int> Sample);

    /// <summary>
    /// One scan of a progressive file: the indices of the frame components it codes, its band and successive approximation,
    /// raw bytes (whole segments) written right before its SOS marker, and for an AC scan optionally the AC symbols every block
    /// codes instead of EOB runs over all blocks. Each symbol is followed by its amplitude bits, all ones (in a refinement scan,
    /// Ah &gt; 0, one sign bit 1 whatever its size); EOB0 (0x00) is not added, the caller lists it where the block should end.
    /// </summary>
    internal readonly record struct ProgressiveScan(int[] Components, int Ss, int Se, int Ah, int Al, byte[]? SegmentsBefore = null, byte[]? AcSymbols = null);

    /// <param name="restartInterval">MCUs between RST markers; 0 writes no DRI.</param>
    /// <param name="acZeroIsCoefficient">
    /// The AC table maps code <c>0</c> to symbol 0x01 and <c>10</c> to EOB, so zero fill bits decode as coefficients;
    /// otherwise its only code, <c>0</c>, is <paramref name="acEndSymbol"/>.
    /// </param>
    /// <param name="truncateAfterMcus">Stop the scan after this many MCUs (padding with 1 bits, then EOI); -1 writes all.</param>
    /// <param name="dcCodeCount">
    /// Four-bit DC codes for the sizes 0..count-1. The default 12 (0000..1011) leaves 11xx undefined; 16 includes the
    /// reserved all-ones code 1111.
    /// </param>
    /// <param name="frameMarker">The SOF marker: 0xC0 baseline, 0xC1 extended sequential.</param>
    /// <param name="huffmanTableId">The destination of both Huffman tables, which every scan component selects.</param>
    /// <param name="acEndSymbol">The symbol of the single AC code <c>0</c> that ends each block; 0x00 is EOB.</param>
    /// <param name="firstDcSize">
    /// When not -1, the first block's DC difference is written with this size category (its largest positive value) instead of
    /// the sample's; meant for one-block images.
    /// </param>
    /// <param name="padWithZeros">Pad each entropy-coded segment to a byte with 0 bits instead of 1 bits.</param>
    /// <param name="blockAcSymbols">
    /// When set, every block codes exactly these AC symbols after its DC difference, each with its amplitude bits all ones (the
    /// largest positive value); EOB (0x00) is not added, the caller lists it where the block should end. The AC table gives these
    /// symbols 8-bit codes, in order of first use. Overrides <paramref name="acZeroIsCoefficient"/> and <paramref name="acEndSymbol"/>.
    /// </param>
    public static byte[] Build(int width, int height, IReadOnlyList<Component> components, int restartInterval = 0,
        bool acZeroIsCoefficient = false, int truncateAfterMcus = -1, int dcCodeCount = 12, byte frameMarker = Sof0,
        int huffmanTableId = 0, byte acEndSymbol = 0x00, int firstDcSize = -1, bool padWithZeros = false, byte[]? blockAcSymbols = null)
    {
        var output = new List<byte> { 0xFF, 0xD8 };

        WriteSegment(output, App0, [(byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0]);
        WriteSegment(output, Dqt, [0, .. Enumerable.Repeat((byte)8, 64)]);

        var dcCounts = new byte[16];
        dcCounts[3] = (byte)dcCodeCount;
        WriteSegment(output, Dht, [(byte)huffmanTableId, .. dcCounts, .. Enumerable.Range(0, dcCodeCount).Select(i => (byte)i)]);

        var acCounts = new byte[16];
        byte[] acSymbols;
        if (blockAcSymbols is not null)
        {
            acSymbols = [.. blockAcSymbols.Distinct()];
            acCounts[7] = (byte)acSymbols.Length;
        }
        else if (acZeroIsCoefficient)
        {
            acCounts[0] = 1;
            acCounts[1] = 1;
            acSymbols = [0x01, 0x00];
        }
        else
        {
            acCounts[0] = 1;
            acSymbols = [acEndSymbol];
        }

        WriteSegment(output, Dht, [(byte)(0x10 | huffmanTableId), .. acCounts, .. acSymbols]);

        if (restartInterval > 0)
            WriteSegment(output, Dri, [(byte)(restartInterval >> 8), (byte)restartInterval]);

        var frame = new List<byte> { 8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, (byte)components.Count };
        foreach (var component in components)
            frame.AddRange([component.Id, (byte)(component.H << 4 | component.V), 0]);

        WriteSegment(output, frameMarker, frame);

        var scan = new List<byte> { (byte)components.Count };
        foreach (var component in components)
            scan.AddRange([component.Id, (byte)(huffmanTableId << 4 | huffmanTableId)]);

        scan.AddRange([0, 63, 0]);
        WriteSegment(output, Sos, scan);

        int maxH = components.Max(c => c.H), maxV = components.Max(c => c.V);
        int mcusPerLine = (width + 8 * maxH - 1) / (8 * maxH), mcusPerColumn = (height + 8 * maxV - 1) / (8 * maxV);
        var predictors = new int[components.Count];
        var bits = new BitWriter(padWithZeros);
        var mcu = 0;
        var restart = 0;
        var truncated = false;

        for (var mcuRow = 0; mcuRow < mcusPerColumn && !truncated; mcuRow++)
        for (var mcuColumn = 0; mcuColumn < mcusPerLine && !truncated; mcuColumn++)
        {
            if (restartInterval > 0 && mcu > 0 && mcu % restartInterval == 0)
            {
                bits.Flush();
                bits.Bytes.AddRange([0xFF, (byte)(0xD0 + (restart++ & 7))]);
                Array.Clear(predictors);
            }

            // The RST marker that would precede the cut MCU is still written, as the reference builder does.
            if (mcu == truncateAfterMcus)
            {
                truncated = true;
                break;
            }

            mcu++;
            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];
                for (var v = 0; v < component.V; v++)
                for (var h = 0; h < component.H; h++)
                {
                    if (firstDcSize >= 0 && mcu == 1 && i == 0 && v == 0 && h == 0)
                    {
                        bits.Write(firstDcSize, 4);
                        bits.Write((1 << firstDcSize) - 1, firstDcSize);
                        predictors[i] = (1 << firstDcSize) - 1;
                    }
                    else
                    {
                        var dc = Math.Clamp(component.Sample(mcuRow * component.V + v, mcuColumn * component.H + h), 0, 255) - 128;
                        var difference = dc - predictors[i];
                        predictors[i] = dc;

                        var size = 0;
                        for (var magnitude = Math.Abs(difference); magnitude > 0; magnitude >>= 1)
                            size++;

                        bits.Write(size, 4);
                        if (size > 0)
                            bits.Write(difference >= 0 ? difference : difference + (1 << size) - 1, size);
                    }

                    if (blockAcSymbols is not null)
                    {
                        foreach (var symbol in blockAcSymbols)
                        {
                            bits.Write(Array.IndexOf(acSymbols, symbol), 8);
                            WriteOnes(bits, symbol & 15);
                        }
                    }
                    else if (acZeroIsCoefficient)
                    {
                        bits.Write(0b10, 2);
                    }
                    else
                    {
                        bits.Write(0, 1);
                    }
                }
            }
        }

        bits.Flush();
        output.AddRange(bits.Bytes);
        output.AddRange([0xFF, 0xD9]);
        return output.ToArray();
    }

    /// <summary>
    /// A progressive (SOF2) image whose coefficients are all zero (flat mid-grey), coded by <paramref name="scans"/>: DC scans
    /// write one bit per block (the category-0 code, or a refinement bit 0), AC scans cover every block with EOB runs.
    /// Every component is sampled 1x1 with ids 1, 2, 3; DC table 0 has the single code <c>0</c> (category 0) and AC table 0
    /// the four-bit codes 0000..1110 for EOB0..EOB14.
    /// </summary>
    /// <param name="componentCount">1 (grayscale) or 3.</param>
    public static byte[] BuildProgressive(int width, int height, int componentCount, IReadOnlyList<ProgressiveScan> scans)
    {
        var output = new List<byte> { 0xFF, 0xD8 };

        WriteSegment(output, App0, [(byte)'J', (byte)'F', (byte)'I', (byte)'F', 0, 1, 1, 0, 0, 1, 0, 1, 0, 0]);
        output.AddRange(QuantizationSegment(8));

        var dcCounts = new byte[16];
        dcCounts[0] = 1;
        WriteSegment(output, Dht, [0x00, .. dcCounts, 0x00]);

        // EOB0..EOB14 take the four-bit codes 0000..1110; the scans' other AC symbols follow as eight-bit codes 11110000 on.
        var extraSymbols = scans.SelectMany(scan => scan.AcSymbols ?? []).Where(symbol => !IsEobRun(symbol)).Distinct().ToArray();
        var acCounts = new byte[16];
        acCounts[3] = 15;
        acCounts[7] = (byte)extraSymbols.Length;
        WriteSegment(output, Dht, [0x10, .. acCounts, .. Enumerable.Range(0, 15).Select(run => (byte)(run << 4)), .. extraSymbols]);

        var frame = new List<byte> { 8, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width, (byte)componentCount };
        for (var i = 0; i < componentCount; i++)
            frame.AddRange([(byte)(i + 1), 0x11, 0]);

        WriteSegment(output, Sof2, frame);

        var blocks = (width + 7) / 8 * ((height + 7) / 8);
        foreach (var scan in scans)
        {
            output.AddRange(scan.SegmentsBefore ?? []);

            var header = new List<byte> { (byte)scan.Components.Length };
            foreach (var component in scan.Components)
                header.AddRange([(byte)(component + 1), 0x00]);

            header.AddRange([(byte)scan.Ss, (byte)scan.Se, (byte)(scan.Ah << 4 | scan.Al)]);
            WriteSegment(output, Sos, header);

            var bits = new BitWriter(padWithZeros: false);
            if (scan.Ss == 0)
            {
                for (var i = 0; i < blocks * scan.Components.Length; i++)
                    bits.Write(0, 1);
            }
            else if (scan.AcSymbols is { } symbols)
            {
                for (var i = 0; i < blocks; i++)
                foreach (var symbol in symbols)
                {
                    if (IsEobRun(symbol))
                    {
                        // EOBr: its four-bit code r, then r extra bits (zeros: a run of 2^r blocks).
                        bits.Write(symbol >> 4, 4);
                        bits.Write(0, symbol >> 4);
                        continue;
                    }

                    bits.Write(0xF0 + Array.IndexOf(extraSymbols, symbol), 8);
                    WriteOnes(bits, scan.Ah > 0 ? Math.Min(symbol & 15, 1) : symbol & 15);
                }
            }
            else
            {
                for (var left = blocks; left > 0;)
                {
                    var run = Math.Min(left, MaxEobRun);
                    var category = 31 - System.Numerics.BitOperations.LeadingZeroCount((uint)run);
                    bits.Write(category, 4);
                    if (category > 0)
                        bits.Write(run - (1 << category), category);

                    left -= run;
                }
            }

            bits.Flush();
            output.AddRange(bits.Bytes);
        }

        output.AddRange([0xFF, 0xD9]);
        return output.ToArray();
    }

    /// <summary>A DQT segment, marker included, defining table 0 with every step equal to <paramref name="step"/>.</summary>
    public static byte[] QuantizationSegment(byte step)
    {
        var output = new List<byte>();
        WriteSegment(output, Dqt, [0, .. Enumerable.Repeat(step, 64)]);
        return output.ToArray();
    }

    /// <summary>A progressive AC symbol with no amplitude bits and a run below 15: EOB0..EOB14.</summary>
    private static bool IsEobRun(byte symbol) => (symbol & 15) == 0 && symbol >> 4 < 15;

    /// <summary><paramref name="count"/> bits of 1: the largest positive amplitude of that size, or a refinement sign bit.</summary>
    private static void WriteOnes(BitWriter bits, int count) => bits.Write((1 << count) - 1, count);

    private static void WriteSegment(List<byte> output, byte marker, IReadOnlyCollection<byte> payload)
    {
        var length = payload.Count + 2;
        output.AddRange([0xFF, marker, (byte)(length >> 8), (byte)length]);
        output.AddRange(payload);
    }

    /// <summary>MSB-first bit writer with 0xFF byte stuffing; <see cref="Flush"/> pads with 1 bits, or 0 bits when asked.</summary>
    private sealed class BitWriter(bool padWithZeros = false)
    {
        private int m_accumulator;
        private int m_count;

        public List<byte> Bytes { get; } = [];

        public void Write(int value, int count)
        {
            for (var i = count - 1; i >= 0; i--)
            {
                m_accumulator = m_accumulator << 1 | (value >> i & 1);
                if (++m_count == 8)
                    Emit();
            }
        }

        public void Flush()
        {
            while (m_count != 0)
                Write(padWithZeros ? 0 : 1, 1);
        }

        private void Emit()
        {
            Bytes.Add((byte)m_accumulator);
            if (m_accumulator == 0xFF)
                Bytes.Add(0x00);

            m_accumulator = 0;
            m_count = 0;
        }
    }
}
