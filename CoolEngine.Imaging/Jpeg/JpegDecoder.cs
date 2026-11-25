using System.Buffers.Binary;

namespace CoolEngine.Imaging.Jpeg;

/// <summary>
/// Decodes baseline, extended sequential and progressive Huffman-coded JPEG (8-bit, 1 or 3 components) into RGBA8,
/// following libjpeg's behaviour: the <c>islow</c> inverse DCT, fancy upsampling and its YCbCr tables.
/// </summary>
/// <remarks>
/// <para>
/// Lossless, hierarchical, arithmetic-coded, 12-bit, CMYK/YCCK and DNL-sized images, images over
/// <see cref="ImageDecoder.MaxPixelCount"/> and files with more than 100 scans throw <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// The decoder is strict: nonconforming or damaged data throws <see cref="InvalidDataException"/> instead of being concealed as
/// libjpeg does. The rules (ITU-T T.81): SOI first and EOI required (bytes after EOI are ignored); only markers and 0xFF fill
/// bytes between segments, and no RSTn or TEM outside a scan; valid segment lengths, one frame, valid frame, table and scan
/// headers (no zero quantization step, no all-ones Huffman code); scan components exist in the frame, are listed once and in
/// frame order, at most 10 blocks per MCU in an interleaved scan, Huffman table ids 0–1 in a baseline (SOF0) frame and every
/// table the scan needs defined; sequential scans have Ss = 0, Se = 63, Ah = Al = 0 and code each component in exactly one
/// scan; a progressive file follows T.81 G.1.1.1 (band limits, Al ≤ 13, first and refinement scans in order, AC only after
/// the component's DC first scan), has a DC first scan for every component, and does not alter a component's quantization
/// table between its scans; in the scan data every Huffman code is valid, a DC difference category is at most 11 and an AC
/// coefficient category at most 10, an AC zero run (ZRL included) never passes the block's or band's last coefficient, a new
/// coefficient in a refinement scan has size 1, a sequential scan has no EOBn run codes, no fill bit is consumed (a truncated or
/// cut scan), and every restart interval is used up and followed by the expected RSTm.
/// </para>
/// <para>
/// Accepted on purpose: the padding bits before a marker are not checked for 1s, and 16-bit quantization tables (Pq = 1) in
/// 8-bit files are decoded, because libjpeg writes them.
/// </para>
/// </remarks>
internal static class JpegDecoder
{
    private const int MaxScans = 100;

    // A scan's interleaved MCU holds at most this many blocks (T.81 B.2.3).
    private const int MaxBlocksPerMcu = 10;

    // The largest DC difference category of 8-bit samples (T.81 Table F.1).
    private const int MaxDcSize = 11;

    // The largest AC coefficient category of 8-bit samples (T.81 Table F.2).
    private const int MaxAcSize = 10;

    private const int YCbCrScaleBits = 16;
    private const int YCbCrOneHalf = 1 << (YCbCrScaleBits - 1);

    // Zig-zag index → natural (row-major) index, plus 16 entries of 63 (libjpeg's jpeg_natural_order padding)
    // so a run that passes coefficient 63 in corrupt data lands on 63 instead of outside the block. Defence in depth only:
    // every scan type rejects a run past the block's or band's last coefficient before indexing, so no input reaches it.
    private static ReadOnlySpan<byte> NaturalOrder =>
    [
        0, 1, 8, 16, 9, 2, 3, 10,
        17, 24, 32, 25, 18, 11, 4, 5,
        12, 19, 26, 33, 40, 48, 41, 34,
        27, 20, 13, 6, 7, 14, 21, 28,
        35, 42, 49, 56, 57, 50, 43, 36,
        29, 22, 15, 23, 30, 37, 44, 51,
        58, 59, 52, 45, 38, 31, 39, 46,
        53, 60, 61, 54, 47, 55, 62, 63,
        63, 63, 63, 63, 63, 63, 63, 63,
        63, 63, 63, 63, 63, 63, 63, 63,
    ];

    // libjpeg's build_ycc_rgb_table, indexed by the Cb or Cr sample.
    private static readonly int[] s_crToR = new int[256];
    private static readonly int[] s_cbToB = new int[256];
    private static readonly int[] s_crToG = new int[256];
    private static readonly int[] s_cbToG = new int[256];

    static JpegDecoder()
    {
        for (var i = 0; i < 256; i++)
        {
            var x = i - 128;
            s_crToR[i] = (Fix(1.40200) * x + YCbCrOneHalf) >> YCbCrScaleBits;
            s_cbToB[i] = (Fix(1.77200) * x + YCbCrOneHalf) >> YCbCrScaleBits;
            s_crToG[i] = -Fix(0.71414) * x;
            s_cbToG[i] = -Fix(0.34414) * x + YCbCrOneHalf;
        }
    }

    /// <param name="data">The whole file, starting with SOI.</param>
    /// <param name="flipVertically">Write image row y to output row <c>height - 1 - y</c>.</param>
    public static RgbaImage Decode(ReadOnlySpan<byte> data, bool flipVertically)
    {
        var decoder = new Decoder(data);
        decoder.ReadFile();
        return decoder.CreateImage(flipVertically);
    }

    private static int Fix(double value) => (int)(value * (1 << YCbCrScaleBits) + 0.5);

    private enum ScanMode
    {
        Sequential,
        DcFirst,
        DcRefine,
        AcFirst,
        AcRefine,
    }

    /// <summary>The decoder state for one file: the marker parser, the tables, the frame and the entropy reader.</summary>
    private ref struct Decoder
    {
        private readonly ReadOnlySpan<byte> m_data;
        private readonly HuffmanTable?[] m_dcTables = new HuffmanTable?[4];
        private readonly HuffmanTable?[] m_acTables = new HuffmanTable?[4];
        private readonly ushort[]?[] m_quantizationTables = new ushort[]?[4];

        // Marker parsing.
        private int m_position;
        private int m_restartInterval;
        private int m_scanCount;
        private bool m_hasFrame;
        private bool m_baseline;
        private bool m_progressive;
        private bool m_hasJfif;
        private bool m_hasAdobe;
        private byte m_adobeTransform;

        // Frame.
        private int m_width;
        private int m_height;
        private int m_maxHorizontalFactor;
        private int m_maxVerticalFactor;
        private int m_mcusPerLine;
        private int m_mcusPerColumn;
        private JpegComponent[] m_components = [];

        // Entropy reader: bits right-aligned in m_bitBuffer, m_bitCount of them valid.
        private int m_readPosition;
        private ulong m_bitBuffer;
        private int m_bitCount;
        private bool m_markerReached;
        private int m_eobRun;

        // How many of the m_bitCount buffered bits are zero fill supplied after a marker or the end of the data;
        // they are always the lowest bits of the buffer. Peeking into them is fine; consuming one means the scan's data
        // ended early.
        private int m_fillBits;

        // The RST number expected at the next restart (0..7).
        private int m_nextRestartNumber;

        public Decoder(ReadOnlySpan<byte> data)
        {
            m_data = data;
        }

        /// <summary>Reads every segment up to EOI, decoding each scan into the coefficient grids. Bytes after EOI are ignored.</summary>
        public void ReadFile()
        {
            if (m_data.Length < 2 || m_data[0] != 0xFF || m_data[1] != 0xD8)
                throw new InvalidDataException("Not a JPEG file: it does not start with SOI.");

            m_position = 2;
            while (true)
            {
                var marker = NextMarker();
                if (marker == 0xD9)
                    break;

                switch (marker)
                {
                    case 0xD8:
                        throw new InvalidDataException("Second SOI marker.");
                    case >= 0xD0 and <= 0xD7:
                        // RSTm sits only between the restart intervals of a scan (T.81 B.2.1); the scan reader consumes those.
                        throw new InvalidDataException($"JPEG restart marker RST{marker - 0xD0} outside a scan.");
                    case 0x01:
                        throw new InvalidDataException("JPEG TEM marker between segments.");
                    case 0xC0:
                    case 0xC1:
                    case 0xC2:
                        ReadFrame(ReadSegment(), baseline: marker == 0xC0, progressive: marker == 0xC2);
                        break;
                    case 0xC3:
                    case >= 0xC5 and <= 0xC7:
                    case >= 0xC9 and <= 0xCB:
                    case >= 0xCD and <= 0xCF:
                        throw new NotSupportedException($"JPEG frame type SOF{marker - 0xC0} (lossless, hierarchical or arithmetic-coded) is not supported.");
                    case 0xCC:
                        throw new NotSupportedException("Arithmetic-coded JPEG (DAC) is not supported.");
                    case 0xC4:
                        ReadHuffmanTables(ReadSegment());
                        break;
                    case 0xDB:
                        ReadQuantizationTables(ReadSegment());
                        break;
                    case 0xDD:
                        ReadRestartInterval(ReadSegment());
                        break;
                    case 0xDA:
                        ReadScan(ReadSegment());
                        break;
                    case 0xE0:
                        ReadJfif(ReadSegment());
                        break;
                    case 0xEE:
                        ReadAdobe(ReadSegment());
                        break;
                    default:
                        // Other APPn, COM and unknown segments.
                        ReadSegment();
                        break;
                }
            }

            if (!m_hasFrame)
                throw new InvalidDataException("JPEG has no frame (SOF) segment.");

            if (m_scanCount == 0)
                throw new InvalidDataException("JPEG has no scan (SOS) segment.");

            // Sequential: each component in exactly one scan (a second one is rejected at its header). Progressive: a DC
            // first scan for every component.
            foreach (var component in m_components)
            {
                if (!m_progressive && component.ScanCount == 0)
                    throw new InvalidDataException($"JPEG component {component.Id} is in no scan.");

                if (m_progressive && component.CoefficientBits[0] < 0)
                    throw new InvalidDataException($"Progressive JPEG component {component.Id} has no DC first scan.");
            }
        }

        /// <summary>Runs the inverse DCT, upsamples, converts the colours and crops to the image.</summary>
        public readonly RgbaImage CreateImage(bool flipVertically)
        {
            foreach (var component in m_components)
            {
                component.Quantization ??= m_quantizationTables[component.QuantizationTableId]?.ToArray()
                    ?? throw new InvalidDataException($"Quantization table {component.QuantizationTableId} is not defined.");

                InverseTransform(component);
            }

            var pixels = new Rgba32[m_width * m_height];
            var rows = new byte[m_components.Length][];
            for (var i = 0; i < rows.Length; i++)
                rows[i] = new byte[m_width];

            var isRgb = m_components.Length == 3 && IsRgb();

            for (var y = 0; y < m_height; y++)
            {
                for (var i = 0; i < m_components.Length; i++)
                    Upsample(m_components[i], y, rows[i]);

                var outputRow = pixels.AsSpan((flipVertically ? m_height - 1 - y : y) * m_width, m_width);
                if (m_components.Length == 1)
                {
                    var gray = rows[0];
                    for (var x = 0; x < m_width; x++)
                        outputRow[x] = new Rgba32(gray[x], gray[x], gray[x], 255);
                }
                else if (isRgb)
                {
                    for (var x = 0; x < m_width; x++)
                        outputRow[x] = new Rgba32(rows[0][x], rows[1][x], rows[2][x], 255);
                }
                else
                {
                    for (var x = 0; x < m_width; x++)
                        outputRow[x] = YCbCrToRgba(rows[0][x], rows[1][x], rows[2][x]);
                }
            }

            return new RgbaImage(m_width, m_height, pixels);
        }

        #region Markers and segments

        /// <summary>Reads the marker that must start at <see cref="m_position"/>; fill bytes 0xFF before it are skipped.</summary>
        /// <returns>The marker code, with <see cref="m_position"/> just after it.</returns>
        /// <exception cref="InvalidDataException">The data ends before EOI, or a byte other than a marker sits between segments.</exception>
        private int NextMarker()
        {
            if (m_position >= m_data.Length)
                throw new InvalidDataException("JPEG ends without an EOI marker.");

            if (m_data[m_position] != 0xFF)
                throw new InvalidDataException($"Stray byte 0x{m_data[m_position]:X2} between JPEG segments at offset {m_position}.");

            while (m_position < m_data.Length && m_data[m_position] == 0xFF)
                m_position++;

            if (m_position >= m_data.Length)
                throw new InvalidDataException("JPEG ends without an EOI marker.");

            var marker = m_data[m_position++];
            if (marker == 0x00)
                throw new InvalidDataException($"Stray 0xFF 0x00 between JPEG segments at offset {m_position - 2}.");

            return marker;
        }

        /// <summary>Reads a segment's length and returns its payload (without the length bytes), moving past it.</summary>
        private ReadOnlySpan<byte> ReadSegment()
        {
            if (m_data.Length - m_position < 2)
                throw new InvalidDataException("JPEG segment length is past the end of the data.");

            var length = BinaryPrimitives.ReadUInt16BigEndian(m_data[m_position..]);
            if (length < 2)
                throw new InvalidDataException($"JPEG segment length {length} is below 2.");

            if (length > m_data.Length - m_position)
                throw new InvalidDataException($"JPEG segment of {length} bytes runs past the end of the data.");

            var payload = m_data.Slice(m_position + 2, length - 2);
            m_position += length;
            return payload;
        }

        private void ReadJfif(ReadOnlySpan<byte> segment)
        {
            if (segment.StartsWith("JFIF\0"u8))
                m_hasJfif = true;
        }

        private void ReadAdobe(ReadOnlySpan<byte> segment)
        {
            if (segment.Length >= 12 && segment.StartsWith("Adobe"u8))
            {
                m_hasAdobe = true;
                m_adobeTransform = segment[11];
            }
        }

        private void ReadFrame(ReadOnlySpan<byte> segment, bool baseline, bool progressive)
        {
            if (m_hasFrame)
                throw new InvalidDataException("JPEG has a second frame (SOF) segment.");

            if (segment.Length < 6)
                throw new InvalidDataException("JPEG frame header is too short.");

            if (segment[0] != 8)
                throw new NotSupportedException($"JPEG sample precision {segment[0]} is not supported; only 8 bits.");

            int height = BinaryPrimitives.ReadUInt16BigEndian(segment[1..]);
            int width = BinaryPrimitives.ReadUInt16BigEndian(segment[3..]);
            int count = segment[5];

            if (height == 0)
                throw new NotSupportedException("JPEG height defined by a DNL marker is not supported.");

            if (width == 0)
                throw new InvalidDataException("JPEG width is 0.");

            ImageDecoder.ValidateSize(width, height);

            if (count == 0)
                throw new InvalidDataException("JPEG frame has no components.");

            if (count == 4)
                throw new NotSupportedException("CMYK and YCCK JPEG are not supported.");

            if (count != 1 && count != 3)
                throw new NotSupportedException($"JPEG with {count} components is not supported; only 1 or 3.");

            if (segment.Length != 6 + 3 * count)
                throw new InvalidDataException("JPEG frame header length does not match its component count.");

            var specs = new (byte Id, int H, int V, int Tq)[count];
            int maxH = 1, maxV = 1;
            for (var i = 0; i < count; i++)
            {
                var spec = segment.Slice(6 + 3 * i, 3);
                var id = spec[0];
                int h = spec[1] >> 4, v = spec[1] & 15, tq = spec[2];

                if (h is < 1 or > 4 || v is < 1 or > 4)
                    throw new InvalidDataException($"JPEG component {id} has sampling factors {h}x{v}; each must be 1..4.");

                if (tq > 3)
                    throw new InvalidDataException($"JPEG component {id} uses quantization table {tq}; at most 3.");

                for (var j = 0; j < i; j++)
                {
                    if (specs[j].Id == id)
                        throw new InvalidDataException($"JPEG component id {id} appears twice.");
                }

                specs[i] = (id, h, v, tq);
                maxH = Math.Max(maxH, h);
                maxV = Math.Max(maxV, v);
            }

            foreach (var spec in specs)
            {
                if (maxH % spec.H != 0 || maxV % spec.V != 0)
                    throw new NotSupportedException($"JPEG component {spec.Id} has a non-integral sampling ratio {maxH}/{spec.H} x {maxV}/{spec.V}.");
            }

            m_width = width;
            m_height = height;
            m_maxHorizontalFactor = maxH;
            m_maxVerticalFactor = maxV;
            m_mcusPerLine = (width + 8 * maxH - 1) / (8 * maxH);
            m_mcusPerColumn = (height + 8 * maxV - 1) / (8 * maxV);
            m_baseline = baseline;
            m_progressive = progressive;

            m_components = new JpegComponent[count];
            for (var i = 0; i < count; i++)
            {
                var (id, h, v, tq) = specs[i];
                m_components[i] = new JpegComponent(id, h, v, tq,
                    width: (width * h + maxH - 1) / maxH,
                    height: (height * v + maxV - 1) / maxV,
                    blocksPerLine: m_mcusPerLine * h,
                    blocksPerColumn: m_mcusPerColumn * v);
            }

            m_hasFrame = true;
        }

        private readonly void ReadHuffmanTables(ReadOnlySpan<byte> segment)
        {
            var offset = 0;
            while (offset < segment.Length)
            {
                if (segment.Length - offset < 17)
                    throw new InvalidDataException("JPEG Huffman table header is truncated.");

                int tableClass = segment[offset] >> 4, id = segment[offset] & 15;
                if (tableClass > 1 || id > 3)
                    throw new InvalidDataException($"JPEG Huffman table class {tableClass} id {id} is invalid.");

                var counts = segment.Slice(offset + 1, 16);
                var total = 0;
                foreach (var count in counts)
                    total += count;

                if (total > segment.Length - offset - 17)
                    throw new InvalidDataException("JPEG Huffman table symbols run past the segment.");

                var table = new HuffmanTable(counts, segment.Slice(offset + 17, total), isDc: tableClass == 0);
                (tableClass == 0 ? m_dcTables : m_acTables)[id] = table;
                offset += 17 + total;
            }
        }

        private readonly void ReadQuantizationTables(ReadOnlySpan<byte> segment)
        {
            var offset = 0;
            while (offset < segment.Length)
            {
                int precision = segment[offset] >> 4, id = segment[offset] & 15;
                if (precision > 1 || id > 3)
                    throw new InvalidDataException($"JPEG quantization table precision {precision} id {id} is invalid.");

                var size = 1 + 64 * (precision + 1);
                if (size > segment.Length - offset)
                    throw new InvalidDataException("JPEG quantization table runs past the segment.");

                var table = new ushort[64];
                var values = segment.Slice(offset + 1, size - 1);
                for (var i = 0; i < 64; i++)
                {
                    var step = precision == 0 ? values[i] : BinaryPrimitives.ReadUInt16BigEndian(values[(2 * i)..]);
                    if (step == 0)
                        throw new InvalidDataException($"JPEG quantization table {id} has a zero step at position {i}.");

                    table[NaturalOrder[i]] = step;
                }

                m_quantizationTables[id] = table;
                offset += size;
            }
        }

        private void ReadRestartInterval(ReadOnlySpan<byte> segment)
        {
            if (segment.Length != 2)
                throw new InvalidDataException("JPEG DRI segment must hold exactly 2 bytes.");

            m_restartInterval = BinaryPrimitives.ReadUInt16BigEndian(segment);
        }

        private void ReadScan(ReadOnlySpan<byte> segment)
        {
            if (!m_hasFrame)
                throw new InvalidDataException("JPEG scan (SOS) before the frame (SOF).");

            if (++m_scanCount > MaxScans)
                throw new NotSupportedException($"JPEG with more than {MaxScans} scans is not supported.");

            if (segment.Length < 1)
                throw new InvalidDataException("JPEG scan header is empty.");

            int count = segment[0];
            if (count is < 1 or > 4 || segment.Length != 4 + 2 * count)
                throw new InvalidDataException("JPEG scan header length does not match its component count.");

            var scanComponents = new JpegComponent[count];
            var previousFrameIndex = -1;
            var blocksPerMcu = 0;
            for (var i = 0; i < count; i++)
            {
                var id = segment[1 + 2 * i];
                var frameIndex = Array.FindIndex(m_components, c => c.Id == id);
                if (frameIndex < 0)
                    throw new InvalidDataException($"JPEG scan refers to component {id}, which the frame does not have.");

                var component = m_components[frameIndex];
                if (Array.IndexOf(scanComponents, component, 0, i) >= 0)
                    throw new InvalidDataException($"JPEG scan lists component {id} twice.");

                // T.81 B.2.3: the scan header lists its components in frame order.
                if (frameIndex < previousFrameIndex)
                    throw new InvalidDataException($"JPEG scan lists component {id} out of frame order.");

                int dcId = segment[2 + 2 * i] >> 4, acId = segment[2 + 2 * i] & 15;
                if (dcId > 3 || acId > 3)
                    throw new InvalidDataException($"JPEG scan uses Huffman tables {dcId}/{acId}; at most 3.");

                // T.81 Table B.3: a baseline scan selects tables 0 and 1 only.
                if (m_baseline && (dcId > 1 || acId > 1))
                    throw new InvalidDataException($"Baseline JPEG scan uses Huffman tables {dcId}/{acId}; at most 1.");

                // A sequential component is in exactly one scan.
                if (!m_progressive && component.ScanCount > 0)
                    throw new InvalidDataException($"Sequential JPEG component {id} is in a second scan.");

                // T.81 B.2.4: the quantization table is not altered between progressive scans of a component.
                if (component.Quantization is { } latched && !latched.AsSpan().SequenceEqual(m_quantizationTables[component.QuantizationTableId]))
                    throw new InvalidDataException($"Quantization table {component.QuantizationTableId} changed between scans of JPEG component {id}.");

                component.DcTableId = dcId;
                component.AcTableId = acId;
                scanComponents[i] = component;
                previousFrameIndex = frameIndex;
                blocksPerMcu += component.HorizontalFactor * component.VerticalFactor;
            }

            // T.81 B.2.3: an interleaved MCU holds at most 10 blocks.
            if (count > 1 && blocksPerMcu > MaxBlocksPerMcu)
                throw new InvalidDataException($"JPEG interleaved scan has {blocksPerMcu} blocks per MCU; at most {MaxBlocksPerMcu}.");

            int start = segment[1 + 2 * count], end = segment[2 + 2 * count];
            int high = segment[3 + 2 * count] >> 4, low = segment[3 + 2 * count] & 15;

            if (m_progressive)
            {
                if (start == 0 && end != 0)
                    throw new InvalidDataException("Progressive DC scan must end at coefficient 0.");

                if (start > 0 && (count != 1 || end < start || end > 63))
                    throw new InvalidDataException($"Progressive AC scan {start}..{end} over {count} components is invalid.");

                if (low > 13)
                    throw new InvalidDataException($"Progressive successive approximation Al = {low} exceeds 13.");

                foreach (var component in scanComponents)
                    CheckProgression(component, start, end, high, low);
            }
            else if (start != 0 || end != 63 || high != 0 || low != 0)
            {
                throw new InvalidDataException($"Sequential JPEG scan must have Ss=0, Se=63, Ah=0, Al=0; it has {start}, {end}, {high}, {low}.");
            }

            var needsDc = !m_progressive || (start == 0 && high == 0);
            var needsAc = !m_progressive || start > 0;
            foreach (var component in scanComponents)
            {
                if (needsDc && m_dcTables[component.DcTableId] is null)
                    throw new InvalidDataException($"JPEG scan needs DC Huffman table {component.DcTableId}, which is not defined.");

                if (needsAc && m_acTables[component.AcTableId] is null)
                    throw new InvalidDataException($"JPEG scan needs AC Huffman table {component.AcTableId}, which is not defined.");

                component.Quantization ??= m_quantizationTables[component.QuantizationTableId]?.ToArray()
                    ?? throw new InvalidDataException($"Quantization table {component.QuantizationTableId} is not defined.");

                component.ScanCount++;
            }

            var mode = !m_progressive ? ScanMode.Sequential
                : start == 0 ? (high == 0 ? ScanMode.DcFirst : ScanMode.DcRefine)
                : (high == 0 ? ScanMode.AcFirst : ScanMode.AcRefine);

            DecodeScan(scanComponents, mode, start, end, low);
        }

        /// <summary>
        /// T.81 G.1.1.1.2: a band's first scan (Ah = 0) must not repeat; a refinement scan's Ah must equal the Al of the
        /// previous scan of every coefficient in its band, and its Al must be Ah − 1; AC scans need the component's DC
        /// first scan before them. Records the new Al per coefficient.
        /// </summary>
        private static void CheckProgression(JpegComponent component, int start, int end, int high, int low)
        {
            var bits = component.CoefficientBits;
            if (start > 0 && bits[0] < 0)
                throw new InvalidDataException($"Progressive AC scan of component {component.Id} comes before its DC scan.");

            if (high != 0 && low != high - 1)
                throw new InvalidDataException($"Progressive refinement scan has Ah={high}, Al={low}; Al must be Ah - 1.");

            for (var k = start; k <= end; k++)
            {
                if (high == 0 && bits[k] >= 0)
                    throw new InvalidDataException($"Progressive first scan repeats coefficient {k} of component {component.Id}.");

                if (high != 0 && bits[k] != high)
                    throw new InvalidDataException($"Progressive refinement scan has Ah={high} for coefficient {k} of component {component.Id}, whose last Al is {bits[k]}.");
            }

            for (var k = start; k <= end; k++)
                bits[k] = low;
        }

        #endregion

        #region Scan decoding

        private void DecodeScan(JpegComponent[] components, ScanMode mode, int start, int end, int low)
        {
            m_readPosition = m_position;
            m_bitBuffer = 0;
            m_bitCount = 0;
            m_markerReached = false;
            m_eobRun = 0;
            m_fillBits = 0;
            m_nextRestartNumber = 0;
            foreach (var component in components)
                component.DcPredictor = 0;

            // Non-interleaved: the component's own block grid, one block per MCU. Interleaved: the frame's MCU grid.
            var single = components.Length == 1;
            var mcusPerLine = single ? components[0].OwnBlocksPerLine : m_mcusPerLine;
            var mcuCount = mcusPerLine * (single ? components[0].OwnBlocksPerColumn : m_mcusPerColumn);

            for (var mcu = 0; mcu < mcuCount; mcu++)
            {
                RestartIfDue(mcu, components);

                int mcuRow = mcu / mcusPerLine, mcuColumn = mcu % mcusPerLine;
                if (single)
                {
                    DecodeBlock(components[0], components[0].Block(mcuRow, mcuColumn), mode, start, end, low);
                    continue;
                }

                foreach (var component in components)
                {
                    for (var v = 0; v < component.VerticalFactor; v++)
                    for (var h = 0; h < component.HorizontalFactor; h++)
                    {
                        var block = component.Block(mcuRow * component.VerticalFactor + v, mcuColumn * component.HorizontalFactor + h);
                        DecodeBlock(component, block, mode, start, end, low);
                    }
                }
            }

            // The scan's data must end here: at most the padding bits of its last byte, then a marker.
            RequireMarkerAfterData("after the last MCU of the scan");
            m_position = m_readPosition;
        }

        /// <summary>
        /// Checks that the entropy-coded data is used up: fewer than 8 unread bits (the padding of the last byte) and,
        /// unless the reader has already stopped at one, a marker at the read position.
        /// </summary>
        private readonly void RequireMarkerAfterData(string where)
        {
            if (m_bitCount - m_fillBits >= 8)
                throw new InvalidDataException($"JPEG scan has unused entropy-coded data {where}.");

            if (m_markerReached || m_readPosition >= m_data.Length)
                return;

            var position = m_readPosition;
            if (m_data[position] != 0xFF)
                throw new InvalidDataException($"JPEG scan has unused entropy-coded data {where}.");

            while (position < m_data.Length && m_data[position] == 0xFF)
                position++;

            if (position < m_data.Length && m_data[position] == 0x00)
                throw new InvalidDataException($"JPEG scan has unused entropy-coded data {where}.");
        }

        /// <summary>True when an RST0–RST7 marker, after any fill bytes, starts at <paramref name="position"/>.</summary>
        private readonly bool IsRestartMarkerAt(int position)
        {
            if (position >= m_data.Length || m_data[position] != 0xFF)
                return false;

            while (position < m_data.Length && m_data[position] == 0xFF)
                position++;

            return position < m_data.Length && m_data[position] is >= 0xD0 and <= 0xD7;
        }

        /// <summary>
        /// Before MCU <paramref name="mcu"/>, when a restart interval has just ended: the interval's data must be used up
        /// and followed by the expected RST marker, which is consumed; the bit buffer, the DC predictors and the EOB run reset.
        /// </summary>
        /// <exception cref="InvalidDataException">Unused data before the marker, or no RSTm with the expected m.</exception>
        private void RestartIfDue(int mcu, JpegComponent[] components)
        {
            if (m_restartInterval == 0 || mcu == 0 || mcu % m_restartInterval != 0)
                return;

            RequireMarkerAfterData($"before restart marker {m_nextRestartNumber}");

            if (!IsRestartMarkerAt(m_readPosition))
                throw new InvalidDataException($"JPEG restart marker RST{m_nextRestartNumber} is missing after MCU {mcu - 1}.");

            while (m_data[m_readPosition] == 0xFF)
                m_readPosition++;

            var number = m_data[m_readPosition++] - 0xD0;
            if (number != m_nextRestartNumber)
                throw new InvalidDataException($"JPEG restart marker RST{number} found where RST{m_nextRestartNumber} was expected.");

            m_nextRestartNumber = (m_nextRestartNumber + 1) & 7;
            m_bitBuffer = 0;
            m_bitCount = 0;
            m_fillBits = 0;
            m_markerReached = false;
            m_eobRun = 0;
            foreach (var component in components)
                component.DcPredictor = 0;
        }

        private void DecodeBlock(JpegComponent component, Span<short> block, ScanMode mode, int start, int end, int low)
        {
            switch (mode)
            {
                case ScanMode.Sequential:
                    DecodeSequential(component, block);
                    break;
                case ScanMode.DcFirst:
                    DecodeDcFirst(component, block, low);
                    break;
                case ScanMode.DcRefine:
                    if (ReadBits(1) != 0)
                        block[0] |= (short)(1 << low);
                    break;
                case ScanMode.AcFirst:
                    DecodeAcFirst(component, block, start, end, low);
                    break;
                case ScanMode.AcRefine:
                    DecodeAcRefine(component, block, start, end, low);
                    break;
            }
        }

        private void DecodeSequential(JpegComponent component, Span<short> block)
        {
            var acTable = m_acTables[component.AcTableId]!;

            component.DcPredictor = unchecked(component.DcPredictor + DecodeDcDifference(m_dcTables[component.DcTableId]!));
            block[0] = unchecked((short)component.DcPredictor);

            for (var k = 1; k < 64; k++)
            {
                var symbol = DecodeSymbol(acTable);
                int run = symbol >> 4, bits = symbol & 15;
                if (bits != 0)
                {
                    RequireAcSize(bits);
                    k = SkipZeros(k, run, 63);
                    block[NaturalOrder[k]] = unchecked((short)ReceiveExtend(bits));
                }
                else if (run == 0)
                {
                    break;
                }
                else
                {
                    // T.81 F.1.2.2: besides EOB (0x00) only ZRL (0xF0) has no amplitude bits; EOBn runs are progressive only.
                    if (run != 15)
                        throw new InvalidDataException($"Sequential JPEG scan has the EOB run code 0x{symbol:X2}.");

                    // ZRL: the 16 zero coefficients k..k+15 must all lie in the block.
                    k = SkipZeros(k, 15, 63);
                }
            }
        }

        /// <summary>
        /// The position after a run of <paramref name="run"/> zero coefficients that starts at <paramref name="k"/>: the next
        /// coefficient's position, or the last zero of a ZRL. It must not pass <paramref name="end"/>, the last coefficient of the
        /// block (63) or of the progressive band (Se).
        /// </summary>
        private static int SkipZeros(int k, int run, int end)
        {
            k += run;
            if (k > end)
                throw new InvalidDataException($"JPEG AC run passes the last coefficient {end} of the band.");

            return k;
        }

        /// <summary>An AC coefficient's size category is at most 10 for 8-bit samples (T.81 Table F.2).</summary>
        private static void RequireAcSize(int size)
        {
            if (size > MaxAcSize)
                throw new InvalidDataException($"JPEG AC coefficient category {size} exceeds {MaxAcSize}.");
        }

        private void DecodeDcFirst(JpegComponent component, Span<short> block, int low)
        {
            component.DcPredictor = unchecked(component.DcPredictor + DecodeDcDifference(m_dcTables[component.DcTableId]!));
            block[0] = unchecked((short)(component.DcPredictor << low));
        }

        /// <summary>Decodes one DC difference: its size category (at most 11, T.81 Table F.1), then its amplitude bits.</summary>
        private int DecodeDcDifference(HuffmanTable dcTable)
        {
            var size = DecodeSymbol(dcTable);
            if (size > MaxDcSize)
                throw new InvalidDataException($"JPEG DC difference category {size} exceeds {MaxDcSize}.");

            return size == 0 ? 0 : ReceiveExtend(size);
        }

        private void DecodeAcFirst(JpegComponent component, Span<short> block, int start, int end, int low)
        {
            if (m_eobRun > 0)
            {
                m_eobRun--;
                return;
            }

            var acTable = m_acTables[component.AcTableId]!;
            for (var k = start; k <= end; k++)
            {
                var symbol = DecodeSymbol(acTable);
                int run = symbol >> 4, bits = symbol & 15;
                if (bits != 0)
                {
                    RequireAcSize(bits);
                    k = SkipZeros(k, run, end);
                    block[NaturalOrder[k]] = unchecked((short)(ReceiveExtend(bits) << low));
                }
                else if (run == 15)
                {
                    k = SkipZeros(k, 15, end);
                }
                else
                {
                    m_eobRun = 1 << run;
                    if (run != 0)
                        m_eobRun += ReadBits(run);

                    m_eobRun--;
                    break;
                }
            }
        }

        /// <summary>libjpeg's <c>decode_mcu_AC_refine</c>: one more bit for the coefficients already nonzero, and the newly nonzero ones.</summary>
        private void DecodeAcRefine(JpegComponent component, Span<short> block, int start, int end, int low)
        {
            var positive = 1 << low;
            var negative = -1 << low;
            var k = start;

            if (m_eobRun == 0)
            {
                var acTable = m_acTables[component.AcTableId]!;
                for (; k <= end; k++)
                {
                    var symbol = DecodeSymbol(acTable);
                    int run = symbol >> 4, bits = symbol & 15;
                    var value = 0;

                    if (bits != 0)
                    {
                        // T.81 G.1.2.3: a newly nonzero coefficient has size 1; its sign follows. libjpeg only warns.
                        if (bits != 1)
                            throw new InvalidDataException($"Progressive JPEG refinement codes a new coefficient of size {bits}; it must be 1.");

                        value = ReadBits(1) != 0 ? positive : negative;
                    }
                    else if (run != 15)
                    {
                        m_eobRun = 1 << run;
                        if (run != 0)
                            m_eobRun += ReadBits(run);

                        break;
                    }

                    // Skip `run` zero-history coefficients, refining the nonzero ones passed on the way.
                    do
                    {
                        var position = NaturalOrder[k];
                        if (block[position] != 0)
                        {
                            RefineNonZero(ref block[position], positive, negative);
                        }
                        else
                        {
                            if (--run < 0)
                                break;
                        }

                        k++;
                    }
                    while (k <= end);

                    // The band ran out before the run found its zero-history coefficients: the new coefficient's place, or the
                    // 16th zero of a ZRL (the loop stops on it, so k > end only when it was not found).
                    if (k > end)
                        throw new InvalidDataException($"JPEG AC run passes the last coefficient {end} of the band.");

                    if (value != 0)
                        block[NaturalOrder[k]] = unchecked((short)value);
                }
            }

            if (m_eobRun > 0)
            {
                // The band ends inside an EOB run: only correction bits for the nonzero coefficients left.
                for (; k <= end; k++)
                {
                    var position = NaturalOrder[k];
                    if (block[position] != 0)
                        RefineNonZero(ref block[position], positive, negative);
                }

                m_eobRun--;
            }
        }

        private void RefineNonZero(ref short coefficient, int positive, int negative)
        {
            if (ReadBits(1) == 0 || (coefficient & positive) != 0)
                return;

            coefficient = unchecked((short)(coefficient + (coefficient >= 0 ? positive : negative)));
        }

        #endregion

        #region Entropy reader

        /// <summary>
        /// Tops the bit buffer up to more than 56 bits. <c>0xFF 0x00</c> is a data byte 0xFF; at any other marker or the end
        /// of the data the reader stops (leaving the marker unread) and pads with zero bits, counted in <see cref="m_fillBits"/>,
        /// so a lookup may peek past the data; consuming them throws (<see cref="Consume"/>).
        /// </summary>
        private void FillBits()
        {
            while (m_bitCount <= 56)
            {
                var value = 0;
                var isData = false;
                if (!m_markerReached && m_readPosition < m_data.Length)
                {
                    value = m_data[m_readPosition];
                    if (value != 0xFF)
                    {
                        m_readPosition++;
                        isData = true;
                    }
                    else
                    {
                        var next = m_readPosition + 1;
                        while (next < m_data.Length && m_data[next] == 0xFF)
                            next++;

                        if (next < m_data.Length && m_data[next] == 0x00)
                        {
                            m_readPosition = next + 1;
                            isData = true;
                        }
                        else
                        {
                            m_markerReached = true;
                            value = 0;
                        }
                    }
                }
                else
                {
                    m_markerReached = true;
                }

                if (!isData)
                    m_fillBits += 8;

                m_bitBuffer = m_bitBuffer << 8 | (uint)value;
                m_bitCount += 8;
            }
        }

        /// <summary>Reads <paramref name="count"/> bits (0..16), most significant first.</summary>
        private int ReadBits(int count)
        {
            if (count == 0)
                return 0;

            if (m_bitCount < count)
                FillBits();

            Consume(count);
            return (int)(m_bitBuffer >> m_bitCount) & ((1 << count) - 1);
        }

        private int DecodeSymbol(HuffmanTable table)
        {
            if (m_bitCount < 16)
                FillBits();

            var peek = (int)(m_bitBuffer >> (m_bitCount - 16)) & 0xFFFF;
            if (!table.TryDecode(peek, out var symbol, out var length))
                throw new InvalidDataException("Invalid Huffman code in JPEG scan data.");

            Consume(length);
            return symbol;
        }

        /// <summary>Drops <paramref name="count"/> buffered bits; they must all be scan data, not the zero fill.</summary>
        /// <exception cref="InvalidDataException">The scan's data ended (end of file or a marker) before these bits.</exception>
        private void Consume(int count)
        {
            m_bitCount -= count;
            if (m_bitCount < m_fillBits)
                throw new InvalidDataException("JPEG scan data ends before the scan is complete.");
        }

        /// <summary>Reads <paramref name="size"/> bits and extends them to a signed value (T.81 F.2.2.1 EXTEND).</summary>
        private int ReceiveExtend(int size)
        {
            var value = ReadBits(size);
            return value < 1 << (size - 1) ? value - (1 << size) + 1 : value;
        }

        #endregion

        #region Output

        private static void InverseTransform(JpegComponent component)
        {
            var stride = component.PlaneStride;
            var plane = new byte[stride * component.OwnBlocksPerColumn * 8];
            var quantization = component.Quantization!;

            for (var blockRow = 0; blockRow < component.OwnBlocksPerColumn; blockRow++)
            for (var blockColumn = 0; blockColumn < component.OwnBlocksPerLine; blockColumn++)
            {
                Idct.Transform(component.Block(blockRow, blockColumn), quantization,
                    plane.AsSpan(blockRow * 8 * stride + blockColumn * 8), stride);
            }

            component.Plane = plane;
        }

        /// <summary>
        /// Writes image row <paramref name="y"/> of the component at full resolution, <see cref="m_width"/> samples.
        /// 2×1 and 2×2 (for components more than 2 samples wide) and 1×2 use libjpeg's fancy (triangle) upsampling;
        /// everything else replicates. Neighbours outside the component's own width or height repeat the edge sample.
        /// </summary>
        private readonly void Upsample(JpegComponent component, int y, Span<byte> row)
        {
            var horizontalRatio = m_maxHorizontalFactor / component.HorizontalFactor;
            var verticalRatio = m_maxVerticalFactor / component.VerticalFactor;
            var plane = component.Plane;
            var stride = component.PlaneStride;
            var lastColumn = component.Width - 1;
            var lastRow = component.Height - 1;

            // libjpeg (jinit_upsampler) uses h2v1 and h2v2 fancy upsampling only when the component is more than 2 samples wide.
            var fancy = component.Width > 2;

            switch (horizontalRatio, verticalRatio)
            {
                case (1, 1):
                    plane.AsSpan(y * stride, m_width).CopyTo(row);
                    break;

                case (2, 1) when fancy:
                {
                    var input = plane.AsSpan(y * stride, stride);
                    for (var x = 0; x < m_width; x++)
                    {
                        var i = x >> 1;
                        row[x] = (x & 1) == 0
                            ? (byte)((3 * input[i] + input[Math.Max(i - 1, 0)] + 1) >> 2)
                            : (byte)((3 * input[i] + input[Math.Min(i + 1, lastColumn)] + 2) >> 2);
                    }

                    break;
                }

                case (1, 2):
                {
                    var inputRow = y >> 1;
                    var upper = (y & 1) == 0;
                    var neighbourRow = upper ? Math.Max(inputRow - 1, 0) : Math.Min(inputRow + 1, lastRow);
                    var bias = upper ? 1 : 2;
                    var input = plane.AsSpan(inputRow * stride, stride);
                    var neighbour = plane.AsSpan(neighbourRow * stride, stride);
                    for (var x = 0; x < m_width; x++)
                        row[x] = (byte)((3 * input[x] + neighbour[x] + bias) >> 2);

                    break;
                }

                case (2, 2) when fancy:
                {
                    var inputRow = y >> 1;
                    var neighbourRow = (y & 1) == 0 ? Math.Max(inputRow - 1, 0) : Math.Min(inputRow + 1, lastRow);
                    var input = plane.AsSpan(inputRow * stride, stride);
                    var neighbour = plane.AsSpan(neighbourRow * stride, stride);
                    for (var x = 0; x < m_width; x++)
                    {
                        var i = x >> 1;
                        var columnSum = 3 * input[i] + neighbour[i];
                        if ((x & 1) == 0)
                        {
                            var left = Math.Max(i - 1, 0);
                            row[x] = (byte)((3 * columnSum + 3 * input[left] + neighbour[left] + 8) >> 4);
                        }
                        else
                        {
                            var right = Math.Min(i + 1, lastColumn);
                            row[x] = (byte)((3 * columnSum + 3 * input[right] + neighbour[right] + 7) >> 4);
                        }
                    }

                    break;
                }

                default:
                {
                    // Replication: every other ratio, and 2×1 or 2×2 at most 2 samples wide.
                    var input = plane.AsSpan(y / verticalRatio * stride, stride);
                    for (var x = 0; x < m_width; x++)
                        row[x] = input[x / horizontalRatio];

                    break;
                }
            }
        }

        /// <summary>
        /// libjpeg's order: a JFIF marker means YCbCr; else an Adobe marker means RGB for transform 0 and YCbCr otherwise;
        /// else component ids 'R', 'G', 'B' mean RGB; else YCbCr.
        /// </summary>
        private readonly bool IsRgb()
        {
            if (m_hasJfif)
                return false;

            if (m_hasAdobe)
                return m_adobeTransform == 0;

            return m_components[0].Id == 'R' && m_components[1].Id == 'G' && m_components[2].Id == 'B';
        }

        private static Rgba32 YCbCrToRgba(int luma, int cb, int cr) => new(
            ClampToByte(luma + s_crToR[cr]),
            ClampToByte(luma + ((s_cbToG[cb] + s_crToG[cr]) >> YCbCrScaleBits)),
            ClampToByte(luma + s_cbToB[cb]),
            255);

        private static byte ClampToByte(int value) => (byte)Math.Clamp(value, 0, 255);

        #endregion
    }
}
