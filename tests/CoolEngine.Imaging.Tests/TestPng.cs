using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using CoolEngine.Imaging.Png;

namespace CoolEngine.Imaging.Tests;

/// <summary>Writes small PNG files by hand, for headers and chunks no encoder would produce.</summary>
internal static class TestPng
{
    private static readonly byte[] s_signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// The signature, IHDR (compression, filter and interlace 0), <paramref name="chunksBeforeIdat"/> in order, one IDAT holding
    /// every scanline prefixed with filter byte 0 and deflated with <see cref="ZLibStream"/>, and IEND.
    /// </summary>
    /// <param name="rawScanlines">The unfiltered scanlines, already packed at <paramref name="bitDepth"/>.</param>
    /// <param name="chunksBeforeIdat">Extra chunks; a chunk with <c>CorruptCrc</c> gets a deliberately wrong CRC.</param>
    public static byte[] Build(
        int width,
        int height,
        byte bitDepth,
        byte colorType,
        byte[][] rawScanlines,
        IReadOnlyList<(string Type, byte[] Data, bool CorruptCrc)>? chunksBeforeIdat = null) =>
        Assemble(width, height, bitDepth, colorType, Deflate(rawScanlines), chunksBeforeIdat);

    /// <summary>
    /// Like <see cref="Build"/>, but with the IDAT data given as is, extra chunks after the IDAT, and IEND optional.
    /// </summary>
    /// <param name="imageData">The IDAT chunk's data, written unchanged.</param>
    /// <param name="chunksAfterIdat">Extra chunks between IDAT and IEND.</param>
    /// <param name="writeEnd">Whether the file ends with an IEND chunk.</param>
    public static byte[] Assemble(
        int width,
        int height,
        byte bitDepth,
        byte colorType,
        byte[] imageData,
        IReadOnlyList<(string Type, byte[] Data, bool CorruptCrc)>? chunksBeforeIdat = null,
        IReadOnlyList<(string Type, byte[] Data, bool CorruptCrc)>? chunksAfterIdat = null,
        bool writeEnd = true)
    {
        using var output = new MemoryStream();
        output.Write(s_signature);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = bitDepth;
        header[9] = colorType;
        WriteChunk(output, "IHDR", header, corruptCrc: false);

        foreach (var (type, data, corruptCrc) in chunksBeforeIdat ?? [])
            WriteChunk(output, type, data, corruptCrc);

        WriteChunk(output, "IDAT", imageData, corruptCrc: false);

        foreach (var (type, data, corruptCrc) in chunksAfterIdat ?? [])
            WriteChunk(output, type, data, corruptCrc);

        if (writeEnd)
            WriteChunk(output, "IEND", [], corruptCrc: false);

        return output.ToArray();
    }

    /// <summary>A zlib stream of every scanline prefixed with filter byte 0 (None).</summary>
    public static byte[] Deflate(byte[][] rawScanlines)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            foreach (var scanline in rawScanlines)
            {
                zlib.WriteByte(0);
                zlib.Write(scanline);
            }
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data, bool corruptCrc)
    {
        var typeAndData = new byte[4 + data.Length];
        Encoding.ASCII.GetBytes(type, typeAndData);
        data.CopyTo(typeAndData, 4);

        var crc = Crc32.Compute(typeAndData);
        if (corruptCrc)
            crc = ~crc;

        Span<byte> field = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(field, data.Length);
        output.Write(field);
        output.Write(typeAndData);
        BinaryPrimitives.WriteUInt32BigEndian(field, crc);
        output.Write(field);
    }
}
