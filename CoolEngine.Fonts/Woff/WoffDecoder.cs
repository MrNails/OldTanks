// Follows FreeType 2.13.2 src/sfnt/sfwoff.c (woff_open_font) (FreeType Project License; credit in docs).

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace CoolEngine.Fonts.Woff;

/// <summary>
/// Decodes WOFF 1.0 (W3C Recommendation, https://www.w3.org/TR/WOFF/) into the sfnt image it wraps, which <see cref="FontLoader"/>
/// then parses like any other font.
/// </summary>
/// <remarks>
/// <para>
/// Follows FreeType 2.13.2's <c>src/sfnt/sfwoff.c</c> (<c>woff_open_font</c>) closely, because the tables must read the same
/// bytes FreeType reads, the bytes past a short <c>OS/2</c> table among them: the sfnt directory is in tag order (the WOFF
/// directory's), the table data is laid out in the order of the WOFF file's data (ascending offsets, which the strict layout
/// rule makes the order of the file), each table starts on a four-byte boundary and its padding is zero. The per-table
/// checksums are copied into the records without being checked (Denys, 2026-11-26: "Stop verifying"; the sfnt reader does not
/// check them either).
/// </para>
/// <para>
/// Strict rules (safety-strict, Denys, 2026-11-26, and his "Reject, like the spec" for the W3C "MUST" cases that OTS and
/// DirectWrite reject): whatever affects what the library reads or the bounds it reads within throws
/// <see cref="InvalidDataException"/>, one test each in <c>WoffDecoderTests</c>:
/// the data shorter than the 44-byte header; a signature other than <c>wOFF</c>; a flavor of <c>ttcf</c> (a WOFF holds one
/// font; collections are WOFF2's); a declared length different from the file's; a non-zero <c>reserved</c> field; no tables;
/// the table directory past the end of the file; tags not strictly ascending (which also forbids a repeated tag); a table whose
/// data does not start where the previous one's padded data ends (the first right after the directory), so tables are
/// contiguous, in data order, four-byte aligned and never overlap; padding bytes (after a table, or between the metadata and
/// the private block) that are not zero; a table's compressed length past the end of the file or above its original length; a
/// <c>totalSfntSize</c> that is not 12 + 16 × tables + the original lengths padded to four bytes; a table whose compressed
/// length is below its original length but that deflate cannot produce (more than 1,032 times its compressed length), whose zlib
/// stream does not inflate, whose inflated length is not its original length, or that has bytes after the end of its zlib
/// stream within its compressed length; a metadata block with lengths but no offset, a compressed length without an original
/// length, not starting where the table data ends, or past the end of the file; a private block with a length but no offset,
/// not starting at the four-byte aligned end of what precedes it, or past the end of the file; bytes after the last block. The
/// metadata and private blocks are bounds-checked and otherwise skipped.
/// </para>
/// <para>
/// Library limit (<see cref="NotSupportedException"/>, before anything is allocated for the output): the original lengths add
/// up past <see cref="FontLoader.MaxFileSize"/>.
/// </para>
/// <para>
/// Accepted, because they change nothing the library reads: the version numbers, a <c>flavor</c> the sfnt reader does not know
/// (it refuses that), table checksums of any value, the content of the metadata and private blocks. FreeType and DirectWrite
/// accept the first three; <c>WoffDecoderTests</c> asserts it.
/// </para>
/// <para>
/// Not detected (a known limit, like KI-023 for PNG's zlib streams): a zlib stream that stops after its last data byte, with
/// no end marker or Adler-32 trailer, reads as complete. .NET reports it only through an opt-in, process-wide switch, which
/// this library does not turn on; the inflated bytes are bounded by the table's original length either way, so nothing that is
/// read changes.
/// </para>
/// <para>
/// Work and memory: the output is allocated once, from the checked sum of the original lengths, which is at most 1,032 times
/// the file's length (and never above <see cref="FontLoader.MaxFileSize"/>); each table inflates, in one pass, into its own
/// slice, and no more than one byte past it is ever read, so a stream that expands further stops there. The compressed bytes are
/// copied, one table at a time, into one buffer of the largest compressed table's size (not the whole file). The check for
/// bytes after a zlib stream costs nothing extra: the inflater is handed all of the table's compressed bytes but the last in
/// bulk, then the last byte alone, and the stream ends exactly at the compressed length if and only if it asks for that last
/// byte. The work is linear in the file plus the output. Peak: the output, that buffer, and the caller's copy of the file
/// (<see cref="FontLoader.Load(byte[])"/> clones it): about the file's length twice, plus the output. Stateless: safe on any thread.
/// </para>
/// </remarks>
internal static class WoffDecoder
{
    private const uint Signature = 0x774F4646;     // wOFF
    private const uint CollectionTag = 0x74746366; // ttcf
    private const int HeaderLength = 44;
    private const int DirectoryEntryLength = 20;
    private const int OffsetTableLength = 12;
    private const int RecordLength = 16;

    // The most deflate expands data: a 258-byte match costs two bits in the best case, so 1,032 output bytes per input byte.
    private const long MaxDeflateRatio = 1032;

    private readonly record struct Entry(uint Tag, long Offset, long CompLength, long OrigLength, uint Checksum)
    {
        public long SfntOffset { get; init; }
    }

    /// <param name="data">The whole WOFF file, signature included.</param>
    /// <returns>A complete sfnt image, which <see cref="FontLoader"/> then parses.</returns>
    /// <exception cref="InvalidDataException">The data breaks a strict rule (listed on this class).</exception>
    /// <exception cref="NotSupportedException">The decoded font would pass <see cref="FontLoader.MaxFileSize"/>.</exception>
    public static byte[] Decode(ReadOnlySpan<byte> data) => Decode(data, null);

    /// <summary>What the decoder did, for tests that assert its work.</summary>
    internal sealed class Statistics
    {
        /// <summary>Reads the inflaters made from the compressed bytes.</summary>
        public long InputReads { get; set; }
    }

    internal static byte[] Decode(ReadOnlySpan<byte> data, Statistics? statistics)
    {
        if (data.Length < HeaderLength)
            throw new InvalidDataException($"The WOFF data is {data.Length} bytes long, shorter than its {HeaderLength}-byte header.");

        if (BinaryPrimitives.ReadUInt32BigEndian(data) != Signature)
            throw new InvalidDataException("The WOFF signature is not 'wOFF'.");

        var flavor = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);
        var length = BinaryPrimitives.ReadUInt32BigEndian(data[8..]);
        var tableCount = BinaryPrimitives.ReadUInt16BigEndian(data[12..]);
        var reserved = BinaryPrimitives.ReadUInt16BigEndian(data[14..]);
        var totalSfntSize = BinaryPrimitives.ReadUInt32BigEndian(data[16..]);
        var metaOffset = BinaryPrimitives.ReadUInt32BigEndian(data[24..]);
        var metaLength = BinaryPrimitives.ReadUInt32BigEndian(data[28..]);
        var metaOrigLength = BinaryPrimitives.ReadUInt32BigEndian(data[32..]);
        var privOffset = BinaryPrimitives.ReadUInt32BigEndian(data[36..]);
        var privLength = BinaryPrimitives.ReadUInt32BigEndian(data[40..]);

        if (flavor == CollectionTag)
            throw new InvalidDataException("The WOFF flavor is 'ttcf', but a WOFF holds one font, not a collection.");

        if (length != data.Length)
            throw new InvalidDataException($"The WOFF header gives the length {length}, but the file is {data.Length} bytes long.");

        if (reserved != 0)
            throw new InvalidDataException($"The WOFF reserved field is 0x{reserved:X4}, not 0.");

        if (tableCount == 0)
            throw new InvalidDataException("The WOFF font has no tables.");

        var directoryEnd = HeaderLength + (long)tableCount * DirectoryEntryLength;
        if (directoryEnd > data.Length)
            throw new InvalidDataException($"The WOFF table directory ({tableCount} entries) runs past the end of the file.");

        var entries = ReadDirectory(data, tableCount);
        var order = LayOut(data, entries, directoryEnd, totalSfntSize);

        CheckBlocks(data, order.DataEnd, metaOffset, metaLength, metaOrigLength, privOffset, privLength);

        return Build(data, flavor, entries, order.SfntLength, statistics);
    }

    private static Entry[] ReadDirectory(ReadOnlySpan<byte> data, int tableCount)
    {
        var entries = new Entry[tableCount];
        uint previousTag = 0;
        for (var i = 0; i < tableCount; i++)
        {
            var at = HeaderLength + i * DirectoryEntryLength;
            var tag = BinaryPrimitives.ReadUInt32BigEndian(data[at..]);
            if (i > 0 && tag <= previousTag)
                throw new InvalidDataException($"The WOFF table directory is not in strictly ascending tag order at entry {i} ('{TagText(tag)}').");

            previousTag = tag;
            entries[i] = new Entry(
                tag,
                BinaryPrimitives.ReadUInt32BigEndian(data[(at + 4)..]),
                BinaryPrimitives.ReadUInt32BigEndian(data[(at + 8)..]),
                BinaryPrimitives.ReadUInt32BigEndian(data[(at + 12)..]),
                BinaryPrimitives.ReadUInt32BigEndian(data[(at + 16)..]));
        }

        return entries;
    }

    /// <summary>
    /// Checks the data layout as FreeType does (ascending offsets, each table where the previous one's padded data ends) and
    /// gives every entry its offset in the sfnt image, in that same order.
    /// </summary>
    private static (long DataEnd, long SfntLength) LayOut(ReadOnlySpan<byte> data, Entry[] entries, long directoryEnd, uint totalSfntSize)
    {
        var fileLength = data.Length;
        var order = Enumerable.Range(0, entries.Length).OrderBy(i => entries[i].Offset).ThenBy(i => i).ToArray();

        var fileOffset = directoryEnd;
        var sfntOffset = (long)OffsetTableLength + (long)entries.Length * RecordLength;
        foreach (var i in order)
        {
            var entry = entries[i];
            if (entry.Offset != fileOffset)
                throw new InvalidDataException($"The WOFF table '{TagText(entry.Tag)}' starts at {entry.Offset}, not at {fileOffset}, where the previous table's data ends.");

            if (entry.CompLength > fileLength || entry.Offset > fileLength - entry.CompLength)
                throw new InvalidDataException($"The WOFF table '{TagText(entry.Tag)}' ({entry.CompLength} bytes at {entry.Offset}) runs past the end of the file.");

            if (entry.CompLength > entry.OrigLength)
                throw new InvalidDataException($"The WOFF table '{TagText(entry.Tag)}' is {entry.CompLength} bytes compressed but {entry.OrigLength} bytes originally.");

            if (entry.CompLength < entry.OrigLength && entry.OrigLength > entry.CompLength * MaxDeflateRatio)
                throw new InvalidDataException($"The WOFF table '{TagText(entry.Tag)}' cannot inflate from {entry.CompLength} to {entry.OrigLength} bytes: deflate expands at most {MaxDeflateRatio} times.");

            RequireZero(data, entry.Offset + entry.CompLength, $"after the WOFF table '{TagText(entry.Tag)}'");
            entries[i] = entry with { SfntOffset = sfntOffset };
            fileOffset += Pad4(entry.CompLength);
            sfntOffset += Pad4(entry.OrigLength);

            if (sfntOffset > FontLoader.MaxFileSize)
                throw new NotSupportedException($"The decoded WOFF font would be more than the {FontLoader.MaxFileSize}-byte limit.");
        }

        if (sfntOffset != totalSfntSize)
            throw new InvalidDataException($"The WOFF header gives the sfnt size {totalSfntSize}, but its tables make {sfntOffset} bytes.");

        return (fileOffset, sfntOffset);
    }

    /// <summary>Bytes from <paramref name="from"/> to the next four-byte boundary are padding and must be zero.</summary>
    private static void RequireZero(ReadOnlySpan<byte> data, long from, string where)
    {
        var end = Pad4(from);
        for (var i = from; i < end && i < data.Length; i++)
        {
            if (data[(int)i] != 0)
                throw new InvalidDataException($"The padding byte at {i} {where} is not zero.");
        }
    }

    private static void CheckBlocks(ReadOnlySpan<byte> data, long position, uint metaOffset, uint metaLength, uint metaOrigLength,
        uint privOffset, uint privLength)
    {
        var fileLength = data.Length;
        if (metaOffset == 0)
        {
            if (metaLength != 0 || metaOrigLength != 0)
                throw new InvalidDataException("The WOFF metadata block has a length but no offset.");
        }
        else
        {
            if (metaLength != 0 && metaOrigLength == 0)
                throw new InvalidDataException("The WOFF metadata block is compressed but has no original length.");

            if (metaOffset != position)
                throw new InvalidDataException($"The WOFF metadata block starts at {metaOffset}, not at {position}, where the table data ends.");

            if (metaOffset + (long)metaLength > fileLength)
                throw new InvalidDataException("The WOFF metadata block runs past the end of the file.");

            position += metaLength;
        }

        if (privOffset == 0)
        {
            if (privLength != 0)
                throw new InvalidDataException("The WOFF private data block has a length but no offset.");
        }
        else
        {
            RequireZero(data, position, "before the WOFF private data block");
            position = Pad4(position);
            if (privOffset != position)
                throw new InvalidDataException($"The WOFF private data block starts at {privOffset}, not at {position}, the four-byte aligned end of the data before it.");

            if (privOffset + (long)privLength > fileLength)
                throw new InvalidDataException("The WOFF private data block runs past the end of the file.");

            position += privLength;
        }

        if (position != fileLength)
            throw new InvalidDataException($"The WOFF file is {fileLength} bytes long, but its blocks end at {position}.");
    }

    private static byte[] Build(ReadOnlySpan<byte> data, uint flavor, Entry[] entries, long sfntLength, Statistics? statistics)
    {
        var sfnt = new byte[sfntLength];

        var entrySelector = 31 - System.Numerics.BitOperations.LeadingZeroCount((uint)entries.Length);
        var searchRange = (1 << entrySelector) * 16;
        BinaryPrimitives.WriteUInt32BigEndian(sfnt, flavor);
        BinaryPrimitives.WriteUInt16BigEndian(sfnt.AsSpan(4), (ushort)entries.Length);
        BinaryPrimitives.WriteUInt16BigEndian(sfnt.AsSpan(6), (ushort)searchRange);
        BinaryPrimitives.WriteUInt16BigEndian(sfnt.AsSpan(8), (ushort)entrySelector);
        BinaryPrimitives.WriteUInt16BigEndian(sfnt.AsSpan(10), (ushort)(entries.Length * 16 - searchRange));

        byte[]? buffer = null;
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var record = sfnt.AsSpan(OffsetTableLength + i * RecordLength);
            BinaryPrimitives.WriteUInt32BigEndian(record, entry.Tag);
            BinaryPrimitives.WriteUInt32BigEndian(record[4..], entry.Checksum);
            BinaryPrimitives.WriteUInt32BigEndian(record[8..], (uint)entry.SfntOffset);
            BinaryPrimitives.WriteUInt32BigEndian(record[12..], (uint)entry.OrigLength);

            var destination = sfnt.AsSpan((int)entry.SfntOffset, (int)entry.OrigLength);
            var source = data.Slice((int)entry.Offset, (int)entry.CompLength);
            if (entry.CompLength == entry.OrigLength)
            {
                source.CopyTo(destination);
                continue;
            }

            buffer ??= new byte[entries.Where(e => e.CompLength < e.OrigLength).Max(e => e.CompLength)];
            source.CopyTo(buffer);
            Inflate(buffer, (int)entry.CompLength, destination, entry.Tag, statistics);
        }

        return sfnt;
    }

    /// <summary>
    /// Inflates one table's zlib stream (the first <paramref name="length"/> bytes of <paramref name="compressed"/>) into exactly
    /// <paramref name="destination"/>; no more than one byte past it is read. The inflater is given all the compressed bytes
    /// but the last at once and the last alone, so the stream ends where the compressed length does exactly when the inflater
    /// has asked for that last byte.
    /// </summary>
    private static void Inflate(byte[] compressed, int length, Span<byte> destination, uint tag, Statistics? statistics)
    {
        var input = new ChunkStream(compressed, length, length - 1, statistics);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);

        var filled = 0;
        while (filled < destination.Length)
        {
            var read = Read(zlib, destination[filled..], tag);
            if (read == 0)
                throw new InvalidDataException($"The zlib data of the WOFF table '{TagText(tag)}' ends after {filled} of {destination.Length} bytes.");

            filled += read;
        }

        Span<byte> probe = stackalloc byte[1];
        if (Read(zlib, probe, tag) != 0)
            throw new InvalidDataException($"The zlib data of the WOFF table '{TagText(tag)}' inflates to more than its {destination.Length} bytes.");

        if (input.Position != length)
            throw new InvalidDataException($"The WOFF table '{TagText(tag)}' has bytes after its zlib stream, within its compressed length.");
    }

    private static int Read(ZLibStream zlib, Span<byte> buffer, uint tag)
    {
        try
        {
            return zlib.Read(buffer);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException($"The zlib data of the WOFF table '{TagText(tag)}' is damaged: {exception.Message}", exception);
        }
    }

    /// <summary>
    /// Reads from the first <c>length</c> bytes of a buffer: up to <c>bulkLimit</c> in any sizes, then one byte per read.
    /// <see cref="Position"/> is how many bytes the reader has taken.
    /// </summary>
    private sealed class ChunkStream(byte[] buffer, int length, int bulkLimit, Statistics? statistics) : Stream
    {
        private int m_position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => length;

        public override long Position
        {
            get => m_position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] destination, int offset, int count) => Read(destination.AsSpan(offset, count));

        public override int Read(Span<byte> destination)
        {
            var position = m_position;
            var count = Math.Min(destination.Length, length - position);
            if (count > 0)
                count = position < bulkLimit ? Math.Min(count, bulkLimit - position) : 1;

            if (count <= 0)
                return 0;

            if (statistics is not null)
                statistics.InputReads++;

            buffer.AsSpan(position, count).CopyTo(destination);
            m_position = position + count;
            return count;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] source, int offset, int count) => throw new NotSupportedException();
    }

    private static long Pad4(long value) => (value + 3) & ~3L;

    private static string TagText(uint tag)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, tag);
        return Encoding.Latin1.GetString(bytes);
    }
}
