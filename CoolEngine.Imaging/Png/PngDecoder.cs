using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace CoolEngine.Imaging.Png;

/// <summary>
/// Decodes a PNG file into RGBA8: every colour type and bit depth of the PNG specification, palettes, <c>tRNS</c> transparency
/// and Adam7 interlacing. 16-bit samples are rounded to 8 bits; gamma, <c>sRGB</c>, <c>iCCP</c> and every other ancillary chunk
/// are ignored, so pixels come out as stored.
/// </summary>
/// <remarks>
/// <para>
/// Strict: nonconforming or damaged data throws <see cref="InvalidDataException"/>. The rules:
/// every chunk, ancillary ones included, passes its CRC; IHDR comes first, once, with a valid size, bit depth for its colour
/// type, compression, filter and interlace method; PLTE is well formed in every colour type (an error in grayscale, at most
/// 2^depth entries for palette images, ignored after the check in RGB and RGBA), comes before IDAT and only once, and a palette
/// image has one; tRNS comes before IDAT, after PLTE, only once, not in a type with alpha, and is no longer than the palette;
/// IDAT chunks are consecutive; IEND is present and empty; the zlib header is valid (CM 8, CINFO ≤ 7, no FDICT, FCHECK); image
/// data too short for the header's size is rejected before the pixels are allocated; the inflated data holds exactly the
/// image's rows, each with a filter type 0–4; any inflater failure (such as a wrong Adler-32) is rethrown as
/// <see cref="InvalidDataException"/> with the inflater's exception inside.
/// </para>
/// <para>
/// tRNS sample bits above the bit depth are masked off (PNG 3 §11.3.1.1). Unknown ancillary chunks with a valid CRC are skipped.
/// An unknown critical chunk or an image over <see cref="ImageDecoder.MaxPixelCount"/> throws <see cref="NotSupportedException"/>.
/// The caller (<see cref="ImageDecoder"/>) has already checked the 8-byte signature. Stateless: safe on any thread.
/// </para>
/// </remarks>
internal static class PngDecoder
{
    private const int SignatureLength = 8;

    // Length (4), type (4) and CRC (4) around each chunk's data.
    private const int ChunkOverhead = 12;
    private const int HeaderLength = 13;
    private const int MaxPaletteEntries = 256;

    // Deflate's largest expansion (1032:1) plus room for the zlib header, trailer and block headers.
    private const long MaxDeflateRatio = 1032;
    private const long MaxDeflateOverhead = 1024;

    private const uint HeaderType = 0x49484452;       // IHDR
    private const uint PaletteType = 0x504C5445;      // PLTE
    private const uint DataType = 0x49444154;         // IDAT
    private const uint EndType = 0x49454E44;          // IEND
    private const uint TransparencyType = 0x74524E53; // tRNS

    private const int Grayscale = 0;
    private const int Rgb = 2;
    private const int Indexed = 3;
    private const int GrayscaleAlpha = 4;
    private const int Rgba = 6;

    private static readonly Rgba32 s_opaqueBlack = new(0, 0, 0, 255);

    // Adam7 passes: first column and row, column and row steps.
    private static ReadOnlySpan<byte> Adam7StartX => [0, 4, 0, 2, 0, 1, 0];
    private static ReadOnlySpan<byte> Adam7StartY => [0, 0, 4, 0, 2, 0, 1];
    private static ReadOnlySpan<byte> Adam7StepX => [8, 8, 4, 4, 2, 2, 1];
    private static ReadOnlySpan<byte> Adam7StepY => [8, 8, 8, 4, 4, 2, 2];

    /// <param name="data">The whole file, signature included.</param>
    /// <param name="flipVertically">Write row y to row <c>height - 1 - y</c>.</param>
    public static RgbaImage Decode(ReadOnlySpan<byte> data, bool flipVertically)
    {
        if (data.Length < SignatureLength)
            throw new InvalidDataException("PNG data is shorter than its signature.");

        Header? header = null;
        var paletteSeen = false;
        var transparencySeen = false;
        var dataSeen = false;
        var dataEnded = false;
        var ended = false;

        // 256 entries, opaque black past the PLTE entries (libpng's behaviour for an index past the palette).
        Rgba32[]? palette = null;
        var paletteCount = 0;

        // The tRNS colour as raw samples at the image's bit depth: one for grayscale, three for RGB.
        int[]? transparentColor = null;

        using var compressed = new MemoryStream();

        var offset = SignatureLength;
        while (!ended && offset < data.Length)
        {
            var remaining = data.Length - offset;
            if (remaining < ChunkOverhead)
                throw new InvalidDataException("PNG data ends inside a chunk header.");

            var length = BinaryPrimitives.ReadUInt32BigEndian(data[offset..]);
            if (length > int.MaxValue || length > (uint)(remaining - ChunkOverhead))
                throw new InvalidDataException($"PNG chunk length {length} runs past the end of the data.");

            var typeAndData = data.Slice(offset + 4, 4 + (int)length);
            var type = BinaryPrimitives.ReadUInt32BigEndian(typeAndData);
            var chunk = typeAndData[4..];
            var crc = BinaryPrimitives.ReadUInt32BigEndian(data[(offset + 8 + (int)length)..]);
            offset += ChunkOverhead + (int)length;

            // Every chunk is CRC-checked. Bit 5 of the first type byte (lower case) marks an ancillary chunk.
            if (Crc32.Compute(typeAndData) != crc)
                throw new InvalidDataException($"PNG chunk {ChunkName(typeAndData)} fails its CRC check.");

            var critical = (typeAndData[0] & 0x20) == 0;

            if (header is null && type != HeaderType)
                throw new InvalidDataException("The first PNG chunk must be IHDR.");

            if (dataSeen && type != DataType)
                dataEnded = true;

            switch (type)
            {
                case HeaderType:
                    if (header is not null)
                        throw new InvalidDataException("A PNG file has more than one IHDR chunk.");

                    header = ReadHeader(chunk);
                    break;

                case PaletteType:
                    if (dataSeen)
                        throw new InvalidDataException("A PNG PLTE chunk comes after the image data.");

                    if (paletteSeen)
                        throw new InvalidDataException("A PNG file has more than one PLTE chunk.");

                    paletteSeen = true;
                    palette = ReadPalette(chunk, header!.Value, out paletteCount);
                    break;

                case TransparencyType:
                    if (dataSeen)
                        throw new InvalidDataException("A PNG tRNS chunk comes after the image data.");

                    if (transparencySeen)
                        throw new InvalidDataException("A PNG file has more than one tRNS chunk.");

                    transparencySeen = true;
                    transparentColor = ReadTransparency(chunk, header!.Value, palette, paletteCount);
                    break;

                case DataType:
                    if (dataEnded)
                        throw new InvalidDataException("PNG IDAT chunks are not consecutive.");

                    dataSeen = true;
                    compressed.Write(chunk);
                    break;

                case EndType:
                    if (length != 0)
                        throw new InvalidDataException($"PNG IEND has length {length}, expected 0.");

                    ended = true;
                    break;

                default:
                    if (critical)
                        throw new NotSupportedException($"Unknown critical PNG chunk {ChunkName(typeAndData)}.");

                    break;
            }
        }

        if (header is not { } h)
            throw new InvalidDataException("A PNG file has no IHDR chunk.");

        if (!dataSeen)
            throw new InvalidDataException("A PNG file has no IDAT chunk.");

        if (!ended)
            throw new InvalidDataException("A PNG file ends without an IEND chunk.");

        if (h.ColorType == Indexed && palette is null)
            throw new InvalidDataException("A palette PNG has no PLTE chunk.");

        ValidateZlibHeader(compressed.GetBuffer().AsSpan(0, (int)compressed.Length));

        // Deflate expands at most 1032:1; image data too short for the header's size is rejected before the pixels are allocated.
        if (InflatedLength(h) > compressed.Length * MaxDeflateRatio + MaxDeflateOverhead)
            throw new InvalidDataException($"PNG image data of {compressed.Length} bytes is too short for a {h.Width}x{h.Height} image.");

        var pixels = new Rgba32[(long)h.Width * h.Height];
        var format = new PixelFormat(h, palette, transparentColor);

        compressed.Position = 0;
        using var inflater = new ZLibStream(compressed, CompressionMode.Decompress);

        if (h.Interlaced)
        {
            for (var pass = 0; pass < Adam7StartX.Length; pass++)
                DecodePass(inflater, format, Adam7StartX[pass], Adam7StartY[pass], Adam7StepX[pass], Adam7StepY[pass], pixels, flipVertically);
        }
        else
        {
            DecodePass(inflater, format, 0, 0, 1, 1, pixels, flipVertically);
        }

        RequireEndOfImageData(inflater);

        return new RgbaImage(h.Width, h.Height, pixels);
    }

    /// <summary>
    /// After the last row the inflated data must end; reading on also makes the inflater check the stream's Adler-32.
    /// </summary>
    /// <exception cref="InvalidDataException">More inflated data, or an inflater failure.</exception>
    private static void RequireEndOfImageData(Stream inflater)
    {
        int next;
        try
        {
            next = inflater.ReadByte();
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            throw CorruptImageData(exception);
        }

        if (next >= 0)
            throw new InvalidDataException("PNG image data holds more than the image's rows.");
    }

    /// <summary>Reads one scanline, filter byte included, from the inflater.</summary>
    /// <exception cref="InvalidDataException">The inflated data ends before the row, or an inflater failure.</exception>
    private static void ReadRow(Stream inflater, byte[] row)
    {
        try
        {
            inflater.ReadExactly(row);
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("PNG image data ends before the last row.", exception);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            throw CorruptImageData(exception);
        }
    }

    private static InvalidDataException CorruptImageData(Exception inflaterException) =>
        new("PNG image data is not a valid zlib stream.", inflaterException);

    /// <summary>
    /// Checks the zlib header (RFC 1950): deflate (CM 8) with a window of at most 32 KiB (CINFO ≤ 7), no preset dictionary,
    /// and a valid FCHECK. <see cref="ZLibStream"/> would report a preset dictionary with its own exception type.
    /// </summary>
    private static void ValidateZlibHeader(ReadOnlySpan<byte> imageData)
    {
        if (imageData.Length < 2)
            throw new InvalidDataException("PNG image data is too short for a zlib header.");

        int cmf = imageData[0];
        int flg = imageData[1];

        if ((cmf & 0x0F) != 8 || cmf >> 4 > 7 || (flg & 0x20) != 0 || (cmf * 256 + flg) % 31 != 0)
            throw new InvalidDataException($"PNG image data has an invalid zlib header {cmf:X2} {flg:X2}.");
    }

    /// <summary>The bytes the image data inflates to: every scanline of every pass, with its filter byte.</summary>
    private static long InflatedLength(Header header)
    {
        if (!header.Interlaced)
            return PassLength(header, 0, 0, 1, 1);

        long total = 0;
        for (var pass = 0; pass < Adam7StartX.Length; pass++)
            total += PassLength(header, Adam7StartX[pass], Adam7StartY[pass], Adam7StepX[pass], Adam7StepY[pass]);

        return total;
    }

    private static long PassLength(Header header, int startX, int startY, int stepX, int stepY)
    {
        var (passWidth, passHeight) = PassSize(header, startX, startY, stepX, stepY);
        if (passWidth == 0 || passHeight == 0)
            return 0;

        return passHeight * (RowLength(header, passWidth) + 1);
    }

    private static (int Width, int Height) PassSize(Header header, int startX, int startY, int stepX, int stepY) =>
    (
        header.Width > startX ? (header.Width - startX + stepX - 1) / stepX : 0,
        header.Height > startY ? (header.Height - startY + stepY - 1) / stepY : 0
    );

    /// <summary>The bytes of one scanline of <paramref name="width"/> pixels, without its filter byte.</summary>
    private static long RowLength(Header header, int width) => ((long)width * header.Channels * header.BitDepth + 7) / 8;

    private static Header ReadHeader(ReadOnlySpan<byte> chunk)
    {
        if (chunk.Length != HeaderLength)
            throw new InvalidDataException($"PNG IHDR has length {chunk.Length}, expected {HeaderLength}.");

        long width = BinaryPrimitives.ReadUInt32BigEndian(chunk);
        long height = BinaryPrimitives.ReadUInt32BigEndian(chunk[4..]);
        ImageDecoder.ValidateSize(width, height);

        int bitDepth = chunk[8];
        int colorType = chunk[9];
        var validDepth = colorType switch
        {
            Grayscale => bitDepth is 1 or 2 or 4 or 8 or 16,
            Rgb or GrayscaleAlpha or Rgba => bitDepth is 8 or 16,
            Indexed => bitDepth is 1 or 2 or 4 or 8,
            _ => false,
        };

        if (!validDepth)
            throw new InvalidDataException($"PNG bit depth {bitDepth} is not valid for colour type {colorType}.");

        if (chunk[10] != 0)
            throw new InvalidDataException($"Unknown PNG compression method {chunk[10]}.");

        if (chunk[11] != 0)
            throw new InvalidDataException($"Unknown PNG filter method {chunk[11]}.");

        if (chunk[12] > 1)
            throw new InvalidDataException($"Unknown PNG interlace method {chunk[12]}.");

        return new Header((int)width, (int)height, bitDepth, colorType, chunk[12] == 1);
    }

    /// <returns>The 256-entry palette for an indexed image; <see langword="null"/> where PLTE is only a hint (RGB, RGBA).</returns>
    private static Rgba32[]? ReadPalette(ReadOnlySpan<byte> chunk, Header header, out int count)
    {
        count = 0;

        if (header.ColorType is Grayscale or GrayscaleAlpha)
            throw new InvalidDataException("A grayscale PNG has a PLTE chunk.");

        // Every PLTE must be well formed, also where it is only a suggested palette.
        var entries = chunk.Length / 3;
        if (chunk.Length % 3 != 0 || entries < 1 || entries > MaxPaletteEntries)
            throw new InvalidDataException($"PNG PLTE length {chunk.Length} is not 1 to {MaxPaletteEntries} RGB entries.");

        if (header.ColorType is Rgb or Rgba)
            return null;

        if (entries > 1 << header.BitDepth)
            throw new InvalidDataException($"PNG PLTE has {entries} entries, more than bit depth {header.BitDepth} can index.");

        var palette = new Rgba32[MaxPaletteEntries];
        Array.Fill(palette, s_opaqueBlack);

        for (var i = 0; i < entries; i++)
            palette[i] = new Rgba32(chunk[i * 3], chunk[i * 3 + 1], chunk[i * 3 + 2], 255);

        count = entries;
        return palette;
    }

    /// <summary>Applies palette alpha in place, or returns the transparent colour of a grayscale or RGB image.</summary>
    private static int[]? ReadTransparency(ReadOnlySpan<byte> chunk, Header header, Rgba32[]? palette, int paletteCount)
    {
        switch (header.ColorType)
        {
            case Indexed:
                if (palette is null)
                    throw new InvalidDataException("A PNG tRNS chunk comes before PLTE.");

                if (chunk.Length > paletteCount)
                    throw new InvalidDataException($"PNG tRNS has {chunk.Length} entries for a {paletteCount}-entry palette.");

                for (var i = 0; i < chunk.Length; i++)
                    palette[i] = palette[i] with { A = chunk[i] };

                return null;

            case Grayscale:
                if (chunk.Length != 2)
                    throw new InvalidDataException($"PNG tRNS for grayscale has length {chunk.Length}, expected 2.");

                return [TransparentSample(chunk, header.BitDepth)];

            case Rgb:
                if (chunk.Length != 6)
                    throw new InvalidDataException($"PNG tRNS for RGB has length {chunk.Length}, expected 6.");

                return
                [
                    TransparentSample(chunk, header.BitDepth),
                    TransparentSample(chunk[2..], header.BitDepth),
                    TransparentSample(chunk[4..], header.BitDepth),
                ];

            default:
                throw new InvalidDataException($"A PNG of colour type {header.ColorType} has an alpha channel and cannot have a tRNS chunk.");
        }
    }

    /// <summary>One 16-bit tRNS sample with the bits above the image's bit depth masked off (PNG 3 §11.3.1.1).</summary>
    private static int TransparentSample(ReadOnlySpan<byte> field, int bitDepth) =>
        BinaryPrimitives.ReadUInt16BigEndian(field) & ((1 << bitDepth) - 1);

    /// <summary>Inflates, unfilters and stores one pass: the whole image, or one Adam7 pass.</summary>
    private static void DecodePass(Stream inflater, PixelFormat format, int startX, int startY, int stepX, int stepY, Rgba32[] pixels, bool flipVertically)
    {
        var header = format.Header;
        var (passWidth, passHeight) = PassSize(header, startX, startY, stepX, stepY);

        if (passWidth == 0 || passHeight == 0)
            return;

        var bitsPerPixel = header.Channels * header.BitDepth;
        var rowLength = RowLength(header, passWidth);
        if (rowLength + 1 > Array.MaxLength)
            throw new NotSupportedException($"A PNG row of {rowLength} bytes is too long.");

        // Index 0 holds the row's filter type; the previous row starts as zeros for each pass.
        var current = new byte[rowLength + 1];
        var previous = new byte[rowLength + 1];
        var filterStride = Math.Max(1, bitsPerPixel / 8);

        for (var row = 0; row < passHeight; row++)
        {
            ReadRow(inflater, current);
            Unfilter(current[0], current.AsSpan(1), previous.AsSpan(1), filterStride);

            var y = startY + row * stepY;
            var targetRow = flipVertically ? header.Height - 1 - y : y;
            format.ExpandRow(current.AsSpan(1), passWidth, pixels.AsSpan(targetRow * header.Width + startX), stepX);

            (current, previous) = (previous, current);
        }
    }

    private static void Unfilter(byte filter, Span<byte> current, ReadOnlySpan<byte> previous, int stride)
    {
        switch (filter)
        {
            case 0: // None
                break;

            case 1: // Sub
                for (var i = stride; i < current.Length; i++)
                    current[i] += current[i - stride];

                break;

            case 2: // Up
                for (var i = 0; i < current.Length; i++)
                    current[i] += previous[i];

                break;

            case 3: // Average
                for (var i = 0; i < current.Length; i++)
                {
                    var left = i >= stride ? current[i - stride] : 0;
                    current[i] += (byte)((left + previous[i]) >> 1);
                }

                break;

            case 4: // Paeth
                for (var i = 0; i < current.Length; i++)
                {
                    var left = i >= stride ? current[i - stride] : 0;
                    var upperLeft = i >= stride ? previous[i - stride] : 0;
                    current[i] += Paeth(left, previous[i], upperLeft);
                }

                break;

            default:
                throw new InvalidDataException($"Unknown PNG filter type {filter}.");
        }
    }

    private static byte Paeth(int left, int up, int upperLeft)
    {
        var estimate = left + up - upperLeft;
        var toLeft = Math.Abs(estimate - left);
        var toUp = Math.Abs(estimate - up);
        var toUpperLeft = Math.Abs(estimate - upperLeft);

        if (toLeft <= toUp && toLeft <= toUpperLeft)
            return (byte)left;

        return toUp <= toUpperLeft ? (byte)up : (byte)upperLeft;
    }

    private static string ChunkName(ReadOnlySpan<byte> typeAndData) => $"'{Encoding.Latin1.GetString(typeAndData[..4])}'";

    private readonly record struct Header(int Width, int Height, int BitDepth, int ColorType, bool Interlaced)
    {
        public int Channels => ColorType switch
        {
            Grayscale or Indexed => 1,
            GrayscaleAlpha => 2,
            Rgb => 3,
            _ => 4,
        };
    }

    /// <summary>Turns unfiltered scanline samples into RGBA8 pixels.</summary>
    private sealed class PixelFormat(Header header, Rgba32[]? palette, int[]? transparentColor)
    {
        public Header Header { get; } = header;

        /// <param name="row">The unfiltered scanline, without its filter byte.</param>
        /// <param name="count">Pixels in the scanline.</param>
        /// <param name="target">Where the scanline's first pixel goes; later ones every <paramref name="step"/> pixels.</param>
        public void ExpandRow(ReadOnlySpan<byte> row, int count, Span<Rgba32> target, int step)
        {
            var depth = Header.BitDepth;

            switch (Header.ColorType)
            {
                case Grayscale:
                    for (var i = 0; i < count; i++)
                    {
                        var value = Sample(row, i, depth);
                        var gray = To8Bits(value, depth);
                        var alpha = transparentColor is not null && value == transparentColor[0] ? (byte)0 : (byte)255;
                        target[i * step] = new Rgba32(gray, gray, gray, alpha);
                    }

                    break;

                case Rgb:
                    for (var i = 0; i < count; i++)
                    {
                        var r = Sample(row, i * 3, depth);
                        var g = Sample(row, i * 3 + 1, depth);
                        var b = Sample(row, i * 3 + 2, depth);
                        var transparent = transparentColor is not null
                            && r == transparentColor[0] && g == transparentColor[1] && b == transparentColor[2];
                        target[i * step] = new Rgba32(To8Bits(r, depth), To8Bits(g, depth), To8Bits(b, depth), transparent ? (byte)0 : (byte)255);
                    }

                    break;

                case Indexed:
                    for (var i = 0; i < count; i++)
                        target[i * step] = palette![Sample(row, i, depth)];

                    break;

                case GrayscaleAlpha:
                    for (var i = 0; i < count; i++)
                    {
                        var gray = To8Bits(Sample(row, i * 2, depth), depth);
                        target[i * step] = new Rgba32(gray, gray, gray, To8Bits(Sample(row, i * 2 + 1, depth), depth));
                    }

                    break;

                default:
                    for (var i = 0; i < count; i++)
                    {
                        target[i * step] = new Rgba32(
                            To8Bits(Sample(row, i * 4, depth), depth),
                            To8Bits(Sample(row, i * 4 + 1, depth), depth),
                            To8Bits(Sample(row, i * 4 + 2, depth), depth),
                            To8Bits(Sample(row, i * 4 + 3, depth), depth));
                    }

                    break;
            }
        }

        /// <summary>The raw sample number <paramref name="index"/> of a scanline; sub-byte samples are packed from the high bit.</summary>
        private static int Sample(ReadOnlySpan<byte> row, int index, int depth)
        {
            switch (depth)
            {
                case 8:
                    return row[index];

                case 16:
                    return (row[index * 2] << 8) | row[index * 2 + 1];

                default:
                    var bitOffset = index * depth;
                    var shift = 8 - depth - (bitOffset & 7);
                    return (row[bitOffset >> 3] >> shift) & ((1 << depth) - 1);
            }
        }

        private static byte To8Bits(int value, int depth) => depth switch
        {
            1 => (byte)(value * 255),
            2 => (byte)(value * 85),
            4 => (byte)(value * 17),
            8 => (byte)value,
            _ => (byte)((value * 255 + 32895) >> 16),
        };
    }
}
